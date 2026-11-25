from __future__ import annotations

import unittest

import numpy as np
import torch
import torch.nn as nn

from runes import RUNES, variant
from train_lora import (
    CLASSES, CONFIGS, Encoder, LoRALinear, adapter_state, calibrate, create_backbone,
    frozen_digest, measure, merge_state, split_records, triplet_loss,
)


class LoRATests(unittest.TestCase):
    def test_frozen_linear_zero_init_and_merge(self):
        torch.manual_seed(41)
        original = nn.Linear(8, 12)
        x = torch.randn(5, 8)
        baseline = original(x).detach()
        base_weight = original.weight.detach().clone()
        layer = LoRALinear(original, rank=2, alpha=4, dropout=0.0)
        torch.testing.assert_close(layer(x), baseline)
        self.assertFalse(layer.base.weight.requires_grad)
        optimizer = torch.optim.AdamW((layer.lora_a, layer.lora_b), lr=0.01)
        layer(x).square().mean().backward()
        optimizer.step()
        self.assertGreater(float(layer.lora_b.detach().abs().max()), 0)
        torch.testing.assert_close(layer.base.weight, base_weight)
        merged = nn.functional.linear(x, layer.merged_weight(), layer.base.bias)
        torch.testing.assert_close(layer.eval()(x), merged)

    def test_triplet_negative_only_unknowns_and_gradients(self):
        # Two examples per known class, two distinct unknown examples.
        emb = torch.tensor([
            [1.0, 0.0, 0.0], [0.6, 0.8, 0.0],
            [0.0, 1.0, 0.0], [0.1, 0.99, 0.0],
            [0.95, 0.1, 0.0], [-1.0, 0.0, 0.0],
        ], requires_grad=True)
        labels = torch.tensor([0, 0, 1, 1, -1, -1])
        hard, active = triplet_loss(emb, labels, 0.20, "hard")
        semi, _ = triplet_loss(emb, labels, 0.20, "semi-hard")
        self.assertGreater(hard.item(), 0)
        self.assertGreater(active, 0)
        self.assertGreaterEqual(hard.item(), semi.item() - 1e-5)
        hard.backward()
        self.assertTrue(torch.isfinite(emb.grad).all())
        self.assertGreater(emb.grad.abs().sum().item(), 0)

    def test_stratified_split_keeps_final_holdout_out_of_every_fold(self):
        rng = np.random.default_rng(452)
        records = [
            {"id": f"{label}-{index}", "label": label, "too_short": False,
             "points": variant(RUNES[label].points, rng, 1.3)}
            for label in range(len(CLASSES)) for index in range(20)
        ]
        for index in range(20):
            records.append({"id": f"unknown-{index}", "label": -1,
                            "points": rng.uniform(.15, .85, (8, 2)), "too_short": False})
        result = split_records(records, 25112026)
        self.assertEqual(len(result["development"]), 195)
        test = set(result["test"])
        self.assertEqual(len(test), 65)
        all_validation = []
        for fold in result["folds"]:
            train, val = set(fold["train"]), set(fold["val"])
            self.assertEqual(len(train), 130)
            self.assertEqual(len(val), 65)
            self.assertEqual(len(train & val), 0)
            self.assertFalse(test & (train | val))
            all_validation.extend(val)
        self.assertEqual(set(all_validation), set(result["development"]))
        self.assertEqual(len(all_validation), len(set(all_validation)))

    def test_calibration_rejects_wrong_or_unknown(self):
        rows = [
            {"label": 0, "prediction": 0, "score": .96, "gap": .17},
            {"label": 1, "prediction": 1, "score": .93, "gap": .12},
            {"label": 1, "prediction": 0, "score": .84, "gap": .14},
            {"label": -1, "prediction": 2, "score": .88, "gap": .03},
        ]
        result = calibrate(rows)
        self.assertEqual(result["correct_accept"], 2)
        self.assertEqual(result["wrong_accept"], 0)
        self.assertEqual(result["invalid_false_accept"], 0)
        self.assertGreater(result["margin"], 0)
        self.assertEqual(measure(rows, result["threshold"], result["margin"])["correct_accept"], 2)


class BackboneLoRATests(unittest.TestCase):
    def test_adapter_count_and_merged_backbone_parity(self):
        torch.set_num_threads(4)
        config = CONFIGS[1]
        model = create_backbone(config)
        initial = frozen_digest(model)
        params = adapter_state(model)
        self.assertEqual(sum(t.numel() for t in params.values()), 18432)
        self.assertTrue(all("lora_" in name for name in params))
        # Nonzero adapter update, not merely a zero-initialized parity test.
        with torch.no_grad():
            for module in model.modules():
                if isinstance(module, LoRALinear):
                    module.lora_b.normal_(mean=0, std=0.003)
        merged = create_backbone()
        merged.load_state_dict(merge_state(model), strict=True)
        rng = np.random.default_rng(55)
        sample = rng.integers(0, 255, (2, 224, 224), dtype=np.uint8)
        encoder = Encoder("cpu")
        a, b = encoder.encode(model, sample), encoder.encode(merged, sample)
        np.testing.assert_allclose(a, b, atol=2e-5, rtol=2e-4)
        self.assertEqual(frozen_digest(model), initial)
        self.assertEqual(sum(p.numel() for p in merged.parameters()), 5524416)


if __name__ == "__main__":
    unittest.main()
