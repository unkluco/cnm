"use strict";

const byId = id => document.getElementById(id);
const canvas = byId("ink");
const ctx = canvas.getContext("2d");
const pad = byId("drawing-pad");
const RUNES = new Map();
let catalogReady = false;
let defaults = {threshold: 0.92, margin: 0.02};
let presets = [];
let points = [];
let activePointer = null;
let method = "unknown";
let lastResult = null;
let saved = false;
let saving = false;
let requestVersion = 0;
let requestController = null;

const fixed = value => typeof value === "number" && Number.isFinite(value) ? value.toFixed(3) : "—";
const clamp = value => Math.max(0, Math.min(1, value));
const limits = () => ({threshold: Number(byId("threshold").value), margin: Number(byId("margin").value)});

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

async function api(url, body, signal) {
  const options = body === undefined ? {signal} : {
    method: "POST", headers: {"Content-Type": "application/json"}, body: JSON.stringify(body), signal
  };
  const response = await fetch(url, options);
  const result = await response.json();
  if (!response.ok) throw new Error(result.error || "Không thể kết nối Rune Lab.");
  return result;
}

function setConnection(state, text) {
  const node = byId("connection");
  node.className = "connection " + state;
  node.querySelector("span").textContent = text;
}

function updatePadState(state, text) {
  pad.classList.remove("accepted", "rejected", "processing");
  if (state) pad.classList.add(state);
  byId("pad-state").textContent = text;
}

function updateVerdict(state, pill, title, detail, glyph = "◇") {
  byId("verdict").className = "verdict " + state;
  const badge = byId("verdict-pill");
  badge.replaceChildren(element("i"), document.createTextNode(" " + pill));
  byId("verdict-title").textContent = title;
  byId("verdict-detail").textContent = detail;
  byId("verdict-glyph").textContent = glyph;
}

function resizeCanvas() {
  const rect = canvas.getBoundingClientRect();
  const dpr = Math.min(window.devicePixelRatio || 1, 2.5);
  canvas.width = Math.max(1, Math.round(rect.width * dpr));
  canvas.height = Math.max(1, Math.round(rect.height * dpr));
  redraw();
}

function redraw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  byId("pad-placeholder").classList.toggle("hidden", points.length > 0);
  if (!points.length) return;
  const width = canvas.width, height = canvas.height;
  const lineWidth = Math.max(3, width * 0.012);
  ctx.save();
  ctx.lineWidth = lineWidth * 1.5;
  ctx.lineCap = "round";
  ctx.lineJoin = "round";
  ctx.shadowColor = "#70d4dc";
  ctx.shadowBlur = width * .034;
  ctx.strokeStyle = "#89dceb";
  ctx.fillStyle = "#f5e4ab";
  if (points.length === 1) {
    ctx.beginPath();
    ctx.arc(points[0][0] * width, points[0][1] * height, lineWidth * .45, 0, Math.PI * 2);
    ctx.fill();
  } else {
    const path = new Path2D();
    points.forEach(([x, y], index) => {
      if (index === 0) path.moveTo(x * width, y * height);
      else path.lineTo(x * width, y * height);
    });
    ctx.stroke(path);
    ctx.shadowBlur = 0;
    ctx.strokeStyle = "#fae9b6";
    ctx.lineWidth = lineWidth * .75;
    ctx.stroke(path);
  }
  ctx.restore();
}

function resetScores() {
  byId("best-score").textContent = "—";
  byId("score-gap").textContent = "—";
  byId("infer-ms").textContent = "—";
  byId("score-list").replaceChildren(element("div", "empty-scores", "Chưa có nét vẽ để phân tích."));
  byId("preview-image").replaceChildren(element("span", "", "—"));
}

function clear() {
  ++requestVersion;
  if (requestController) requestController.abort();
  const captured = activePointer;
  activePointer = null;
  if (captured !== null && canvas.hasPointerCapture(captured)) canvas.releasePointerCapture(captured);
  points = [];
  lastResult = null;
  saved = false;
  resetScores();
  updateVerdict("idle", "CHỜ VẼ", "Chưa có kết quả", "Nét vẽ sẽ được so sánh với toàn bộ thư viện khi nhấc tay.");
  updatePadState("", "CHỜ NÉT VẼ");
  byId("stroke-stats").textContent = "0 điểm · Chưa vẽ";
  redraw();
  updateSave();
}

function pointerPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return [clamp((event.clientX - rect.left) / rect.width), clamp((event.clientY - rect.top) / rect.height)];
}

function appendPoint(event) {
  const [x, y] = pointerPoint(event);
  const prev = points[points.length - 1];
  if (prev && Math.hypot(prev[0] - x, prev[1] - y) < 0.0009) return;
  if (points.length >= 4096) return;
  points.push([x, y]);
}

canvas.addEventListener("pointerdown", event => {
  if (!catalogReady || activePointer !== null || (event.pointerType === "mouse" && event.button !== 0)) return;
  event.preventDefault();
  clear();
  activePointer = event.pointerId;
  method = event.pointerType || "unknown";
  canvas.setPointerCapture(event.pointerId);
  appendPoint(event);
  updatePadState("", "ĐANG VẼ · NHẤC TAY ĐỂ XÁC NHẬN");
  byId("stroke-stats").textContent = "1 điểm · Đang vẽ";
  redraw();
});

canvas.addEventListener("pointermove", event => {
  if (event.pointerId !== activePointer) return;
  event.preventDefault();
  const batch = event.getCoalescedEvents ? event.getCoalescedEvents() : [];
  for (const sample of batch.length ? batch : [event]) appendPoint(sample);
  byId("stroke-stats").textContent = points.length + " điểm · Đang vẽ";
  redraw();
});

canvas.addEventListener("pointerup", event => {
  if (event.pointerId !== activePointer) return;
  event.preventDefault();
  appendPoint(event);
  activePointer = null;
  if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
  redraw();
  analyze();
});

canvas.addEventListener("pointercancel", event => {
  if (activePointer === event.pointerId) clear();
});
canvas.addEventListener("lostpointercapture", event => {
  if (activePointer === event.pointerId) clear();
});
canvas.addEventListener("contextmenu", event => event.preventDefault());
canvas.addEventListener("keydown", event => { if (event.key === "Escape") clear(); });
byId("clear").addEventListener("click", clear);

function chooseExpected(id) {
  byId("expected").value = id;
  renderSelection();
}

function renderSelection() {
  const id = byId("expected").value;
  for (const button of byId("rune-grid").querySelectorAll(".rune-card")) {
    const selected = button.dataset.rune === id;
    button.classList.toggle("selected", selected);
    button.setAttribute("aria-pressed", String(selected));
  }
  byId("choose-invalid").classList.toggle("selected", id === "invalid");
  const rune = RUNES.get(id);
  const icon = byId("target-icon");
  if (rune) {
    const image = element("img");
    image.src = rune.preview;
    image.alt = rune.name;
    icon.replaceChildren(image);
  } else {
    icon.replaceChildren(element("span", "", id === "invalid" ? "×" : "◇"));
  }
  updateSave();
}
byId("expected").addEventListener("change", renderSelection);
byId("choose-invalid").addEventListener("click", () => chooseExpected("invalid"));

function renderCatalog(data) {
  const grid = byId("rune-grid");
  grid.replaceChildren();
  const select = byId("expected");
  for (const rune of data.runes) {
    RUNES.set(rune.id, rune);
    const button = element("button", "rune-card");
    button.type = "button";
    button.dataset.rune = rune.id;
    button.setAttribute("aria-pressed", "false");
    button.title = "Chọn mẫu " + rune.name;
    const image = element("img");
    image.src = rune.preview;
    image.alt = "";
    button.append(image, element("strong", "", rune.name), element("small", "", rune.intent));
    button.addEventListener("click", () => chooseExpected(rune.id));
    grid.append(button);
    const option = element("option", "", rune.name + " — " + rune.intent);
    option.value = rune.id;
    select.append(option);
  }
  defaults = {threshold: data.threshold, margin: data.margin};
  presets = data.presets || [];
  byId("threshold").value = defaults.threshold;
  byId("margin").value = defaults.margin;
  const buttons = byId("preset-buttons");
  buttons.replaceChildren();
  buttons.hidden = !presets.length;
  for (const preset of presets) {
    const button = element("button", "preset-button");
    button.type = "button";
    button.dataset.preset = preset.id;
    button.append(element("span", "", preset.name), document.createTextNode(preset.threshold.toFixed(2) + " · " + preset.margin.toFixed(2)));
    button.addEventListener("click", () => {
      byId("threshold").value = preset.threshold;
      byId("margin").value = preset.margin;
      updateLimits();
    });
    buttons.append(button);
  }
  updateLimits();
  byId("saved-count").textContent = data.samples_saved;
  const trained = data.model.variant === "lora";
  byId("model-name").textContent = data.model.model;
  byId("model-meta").textContent = "224 × 224 · " + data.model.feature_dim + " chiều · " + (data.model.weights_bytes / 1e6).toFixed(1) + " MB";
  byId("library-count").textContent = data.runes.length + " ký hiệu · " + data.model.references_per_rune + " mẫu / ký hiệu";
  byId("runtime-note").textContent = "Chạy local · " + data.model.device.toUpperCase() + " · " + data.model.reference_count + " vector mẫu · " + (trained ? "LoRA đã gộp" : "Không huấn luyện");
  if (trained) {
    const miningName = data.model.candidate_config?.mining === "semi-hard" ? "Semi-Hard Triplet" : "Batch-Hard Triplet";
    byId("phase-name").textContent = "THỬ NGHIỆM LORA";
    byId("trial-banner").hidden = false;
    byId("trial-description").textContent = data.model.model + " · " + miningName + " · " + data.model.reference_count + " vector mẫu. Thử cả nét đúng và nét sai gần giống ký hiệu.";
    byId("trial-data-count").textContent = data.baseline_samples_saved + " mẫu cũ giữ nguyên · mẫu mới lưu riêng";
    byId("data-title").textContent = "Ghi mẫu thử bản LoRA";
    byId("data-description").textContent = "Lưu riêng dữ liệu mới để kiểm tra model đã train.";
    byId("threshold-description").textContent = "Đã hiệu chỉnh: dễ nhận nét đúng hơn; Chặt hơn: giảm nguy cơ nhận nhầm nét lạ. Chưa phải ngưỡng gameplay chính thức.";
  }
  setConnection("ready", "MODEL ĐÃ SẴN SÀNG");
  catalogReady = true;
  renderSelection();
}

function decision(result) {
  if (!result || !result.scores.length) return {accepted: false, reason: "too_short", gap: null};
  const top = result.scores[0];
  const gap = top.score - (result.scores[1]?.score ?? -1);
  const rule = limits();
  const reason = top.score < rule.threshold ? "below_threshold" : gap < rule.margin ? "ambiguous" : "accepted";
  return {accepted: reason === "accepted", reason, gap, rune_id: reason === "accepted" ? top.id : null};
}

function renderResult() {
  if (!lastResult) return;
  const result = lastResult;
  const scores = result.scores;
  const best = scores[0];
  const judged = decision(result);
  const {threshold, margin} = limits();
  const preview = byId("preview-image");
  if (result.preview) {
    const img = element("img");
    img.src = result.preview;
    img.alt = "Ảnh nét vẽ đã chuẩn hóa mà model nhận";
    preview.replaceChildren(img);
  } else {
    preview.replaceChildren(element("span", "", "—"));
  }
  byId("stroke-stats").textContent = result.stroke.point_count + " điểm · " + result.total_ms.toFixed(1) + " ms tổng";
  byId("best-score").textContent = best ? fixed(best.score) : "—";
  byId("score-gap").textContent = judged.gap === null ? "—" : fixed(judged.gap);
  byId("infer-ms").replaceChildren(document.createTextNode(result.model_ms ? result.model_ms.toFixed(1) : "0"), element("small", "", " ms"));
  const list = byId("score-list");
  list.replaceChildren();
  if (scores.length) {
    scores.forEach((score, index) => {
      const row = element("div", "score-row" + (index === 0 ? " top" : ""));
      const bar = element("progress");
      bar.max = 1;
      bar.value = clamp(score.score);
      bar.setAttribute("aria-label", score.name + " " + fixed(score.score));
      row.append(element("span", "", score.name), bar, element("strong", "", fixed(score.score)));
      list.append(row);
    });
  } else {
    list.append(element("div", "empty-scores", "Loại bỏ nét quá ngắn trước khi chạy model."));
  }
  if (judged.accepted) {
    updateVerdict("accepted", "CHẤP NHẬN", best.name, best.intent + " · Mẫu gần nhất #" + best.reference + ". Hai điều kiện đều đạt.", "◆");
    updatePadState("accepted", "✓ " + best.name.toUpperCase() + " · " + fixed(best.score));
  } else if (judged.reason === "too_short") {
    updateVerdict("rejected", "TỪ CHỐI", "Nét vẽ quá ngắn", "Chạm hoặc nét quá nhỏ không kích hoạt chiêu. Hãy vẽ dài hơn.", "×");
    updatePadState("rejected", "NÉT QUÁ NGẮN");
  } else if (judged.reason === "below_threshold") {
    updateVerdict("rejected", "TỪ CHỐI", "Không đủ tương đồng", "Gần nhất: " + best.name + " (" + fixed(best.score) + ") < " + threshold.toFixed(2) + ".", "×");
    updatePadState("rejected", "× KHÔNG ĐỦ TƯƠNG ĐỒNG · " + fixed(best.score));
  } else {
    updateVerdict("rejected", "TỪ CHỐI", "Nét vẽ mơ hồ", "Gần nhất: " + best.name + " và " + scores[1].name + ". Chênh " + fixed(judged.gap) + " < " + margin.toFixed(2) + ".", "×");
    updatePadState("rejected", "× NÉT MƠ HỒ · " + fixed(judged.gap));
  }
  updateSave();
}

async function analyze() {
  if (!points.length || !catalogReady) return;
  const version = ++requestVersion;
  if (requestController) requestController.abort();
  requestController = new AbortController();
  lastResult = null;
  saved = false;
  updateSave();
  updatePadState("processing", "ĐANG ĐỌC CỔ NGỮ...");
  updateVerdict("processing", "ĐANG PHÂN TÍCH", "Đọc dấu ấn...", "ViT-Tiny đang tạo vector 192 chiều từ nét vẽ.");
  try {
    const result = await api("/api/recognize", {
      points: points.slice(), input_method: method, ...limits()
    }, requestController.signal);
    if (version !== requestVersion) return;
    lastResult = result;
    renderResult();
  } catch (error) {
    if (version !== requestVersion || error.name === "AbortError") return;
    updateVerdict("rejected", "LỖI", "Không thể phân tích", error.message, "×");
    updatePadState("rejected", "THỬ VẼ LẠI");
  }
}

function updateLimits() {
  byId("threshold-value").textContent = Number(byId("threshold").value).toFixed(2);
  byId("margin-value").textContent = Number(byId("margin").value).toFixed(2);
  const current = limits();
  for (const button of byId("preset-buttons").querySelectorAll("button")) {
    const preset = presets.find(p => p.id === button.dataset.preset);
    const selected = preset && Math.abs(current.threshold - preset.threshold) < 1e-6 && Math.abs(current.margin - preset.margin) < 1e-6;
    button.classList.toggle("selected", Boolean(selected));
    button.setAttribute("aria-pressed", String(Boolean(selected)));
  }
  if (lastResult) renderResult();
}
byId("threshold").addEventListener("input", updateLimits);
byId("margin").addEventListener("input", updateLimits);
byId("reset-limits").addEventListener("click", () => {
  byId("threshold").value = defaults.threshold;
  byId("margin").value = defaults.margin;
  updateLimits();
});

function setSaveNote(text, cls = "") {
  byId("save-note").textContent = text;
  byId("save-note").className = cls;
}

function updateSave() {
  const expected = byId("expected").value;
  const participant = byId("participant").value.trim();
  const validParticipant = /^[A-Za-z0-9_.-]{1,40}$/.test(participant);
  byId("save").disabled = !lastResult || !expected || !validParticipant || saved || saving;
  if (saving) setSaveNote("Đang ghi mẫu…");
  else if (saved) setSaveNote("Đã lưu lượt vẽ. Vẽ lại để thu mẫu mới.", "success-text");
  else if (!lastResult) setSaveNote("Chọn ký hiệu, vẽ và điền mã người vẽ.");
  else if (!expected) setSaveNote("Chọn ký hiệu định vẽ hoặc nét không hợp lệ.");
  else if (!validParticipant) setSaveNote("Nhập mã người vẽ (chữ, số, dấu - _ .).");
  else {
    const judged = decision(lastResult);
    const correct = expected === "invalid" ? !judged.accepted : judged.rune_id === expected;
    setSaveNote(correct ? "Kết quả khớp nhãn kiểm thử. Có thể lưu." : "Kết quả khác nhãn. Vẫn lưu để đo sai số.", correct ? "success-text" : "error-text");
  }
}
byId("participant").addEventListener("input", updateSave);
byId("save").addEventListener("click", async () => {
  if (!lastResult || saving || saved) return;
  saving = true;
  const attempt = lastResult.id;
  updateSave();
  try {
    const result = await api("/api/samples", {
      id: attempt, expected: byId("expected").value, participant: byId("participant").value.trim(), ...limits()
    });
    byId("saved-count").textContent = result.count;
    if (lastResult?.id === attempt) saved = true;
    saving = false;
    updateSave();
  } catch (error) {
    saving = false;
    updateSave();
    setSaveNote(error.message, "error-text");
  }
});

new ResizeObserver(resizeCanvas).observe(pad);
resizeCanvas();
api("/api/catalog").then(renderCatalog).catch(error => {
  setConnection("error", "MẤT KẾT NỐI");
  byId("global-error").hidden = false;
  byId("global-error").textContent = "Không tải được ViT-Tiny: " + error.message;
});
