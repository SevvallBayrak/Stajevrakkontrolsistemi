(function () {
  "use strict";
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const csrf = () => ($('meta[name="csrf-token"]') || {}).content || "";

  /* ---------------- Toasts ---------------- */
  $$(".toast").forEach((t, i) => {
    const close = () => { t.style.transition = "all .25s"; t.style.opacity = 0; t.style.transform = "translateX(20px)"; setTimeout(() => t.remove(), 260); };
    $(".close", t) && $(".close", t).addEventListener("click", close);
    if (!t.classList.contains("error") && !t.classList.contains("locked")) setTimeout(close, 5000 + i * 600);
  });

  /* ---------------- Modal ---------------- */
  const modal = $("#modal");
  window.showModal = function (title, html, opts = {}) {
    if (!modal) return alert(title + "\n" + html.replace(/<[^>]+>/g, ""));
    $("#modal-title").textContent = title;
    $("#modal-body").innerHTML = html;
    const ok = $("#modal-ok"), cancel = $("#modal-cancel");
    ok.textContent = opts.okText || "Tamam";
    ok.className = "btn " + (opts.danger ? "btn-primary" : "btn-dark");
    cancel.style.display = opts.onConfirm ? "" : "none";
    ok.onclick = () => { closeModal(); opts.onConfirm && opts.onConfirm(); };
    modal.classList.add("open");
    setTimeout(() => ok.focus(), 50);
  };
  function closeModal() { modal && modal.classList.remove("open"); }
  if (modal) {
    $("#modal-cancel").addEventListener("click", closeModal);
    modal.addEventListener("click", (e) => { if (e.target === modal) closeModal(); });
    document.addEventListener("keydown", (e) => { if (e.key === "Escape") closeModal(); });
  }
  // Sunucudan gelen pop-up uyarıları (ör. hatalı dosya formatı, geçersiz URL)
  $$("[data-popup]").forEach((el) => showModal(el.dataset.title || "Uyarı", el.innerHTML));

  /* ---------------- Onaylı formlar (silme vb.) ---------------- */
  $$("form[data-confirm]").forEach((f) => {
    f.addEventListener("submit", (e) => {
      if (f.dataset.confirmed) return;
      e.preventDefault();
      showModal(f.dataset.confirmTitle || "Emin misiniz?", f.dataset.confirm, {
        danger: true, okText: f.dataset.confirmOk || "Evet, devam et",
        onConfirm: () => { f.dataset.confirmed = "1"; f.submit(); },
      });
    });
  });

  /* ---------------- Şifre göster/gizle + güç göstergesi ---------------- */
  $$(".pw-toggle").forEach((b) => b.addEventListener("click", () => {
    const input = b.parentElement.querySelector("input");
    input.type = input.type === "password" ? "text" : "password";
  }));
  const pw = $("#password"), meter = $("#pw-meter");
  if (pw && meter) pw.addEventListener("input", () => {
    const v = pw.value; let s = 0;
    if (v.length >= 8) s++; if (/[A-ZÇĞİÖŞÜ]/.test(v)) s++; if (/\d/.test(v)) s++; if (/[^\w]/.test(v)) s++; if (v.length >= 12) s++;
    meter.style.width = (s / 5 * 100) + "%";
    meter.style.background = s <= 2 ? "#d4142a" : s <= 3 ? "#f5a623" : "#157a4a";
  });

  /* ---------------- Evrak yükleme: istemci tarafı ön kontrol ---------------- */
  $$("[data-dropzone]").forEach((zone) => {
    const input = $("input[type=file]", zone);
    const form = zone.closest("form");
    const allowed = (zone.dataset.allowed || "").split(",");
    const maxBytes = parseInt(zone.dataset.max || "0", 10);
    const chosen = $(".file-chosen", form);
    const submit = $("button[type=submit]", form);
    const checks = $$(".checks li", form);

    const setCheck = (i, state) => { if (checks[i]) { checks[i].classList.remove("pass", "fail"); state && checks[i].classList.add(state); } };
    const reset = () => { checks.forEach((_, i) => setCheck(i)); chosen && chosen.classList.remove("show"); submit && (submit.disabled = true); };

    function inspect(file) {
      reset();
      if (!file) return;
      const ext = "." + (file.name.split(".").pop() || "").toLowerCase();
      chosen && chosen.classList.add("show");
      if (chosen) { $(".ext", chosen).textContent = ext.replace(".", ""); $(".name", chosen).textContent = file.name; $(".size", chosen).textContent = (file.size / 1024 / 1024).toFixed(2) + " MB"; }
      setCheck(0, file.size > 0 ? "pass" : "fail");
      const extOk = allowed.includes(ext);
      setCheck(1, extOk ? "pass" : "fail");
      const sizeOk = !maxBytes || file.size <= maxBytes;
      setCheck(2, sizeOk ? "pass" : "fail");
      if (!extOk) {
        input.value = ""; submit && (submit.disabled = true);
        return showModal("Desteklenmeyen dosya formatı", `<p><strong>${escapeHtml(file.name)}</strong> yüklenemez.</p><p class="muted">Kabul edilen formatlar: <strong>${zone.dataset.allowedLabel || allowed.join(", ")}</strong>. Diğer formatlar sistem tarafından reddedilir.</p>`);
      }
      if (!sizeOk) {
        input.value = ""; submit && (submit.disabled = true);
        return showModal("Dosya çok büyük", `<p>Dosya boyutu en fazla <strong>${(maxBytes / 1048576).toFixed(0)} MB</strong> olabilir.</p>`);
      }
      submit && (submit.disabled = false);
    }
    input.addEventListener("change", () => inspect(input.files[0]));
    ["dragenter", "dragover"].forEach((ev) => zone.addEventListener(ev, (e) => { e.preventDefault(); zone.classList.add("drag"); }));
    ["dragleave", "drop"].forEach((ev) => zone.addEventListener(ev, () => zone.classList.remove("drag")));
    zone.addEventListener("drop", (e) => {
      e.preventDefault();
      if (e.dataTransfer.files.length) { input.files = e.dataTransfer.files; inspect(input.files[0]); }
    });
    form.addEventListener("submit", () => { submit && (submit.disabled = true, submit.innerHTML = "Yükleniyor ve analiz ediliyor…"); });
    reset();
  });

  /* ---------------- Staj günü hesaplayıcı ---------------- */
  const calcBtn = $("#calc-days");
  if (calcBtn) calcBtn.addEventListener("click", async () => {
    const fd = new FormData();
    fd.append("start", $("#staj_baslangic").value);
    fd.append("end", $("#staj_bitis").value);
    $$('input[name="staj_gunleri"]:checked').forEach((c) => fd.append("days", c.value));
    fd.append("csrf_token", csrf());
    const out = $("#calc-result");
    try {
      const res = await fetch(calcBtn.dataset.url, { method: "POST", body: fd, headers: { "X-CSRF-Token": csrf() } });
      const data = await res.json();
      if (!data.ok) { out.innerHTML = `<span class="muted">${data.message}</span>`; return; }
      const hol = data.skippedHolidays.length ? `<span class="muted">Hesaptan çıkarılan resmi tatiller: ${data.skippedHolidays.map((h) => h.date + " (" + h.name + ")").join(", ")}</span>` : `<span class="muted">Aralıkta seçili günlere denk gelen resmi tatil yok.</span>`;
      out.innerHTML = `<span class="big">${data.count}</span><span>iş günü hesaplandı<br><span class="muted">${data.first || "-"} → ${data.last || "-"}</span></span>${hol}<button type="button" class="btn btn-sm btn-ghost" id="use-calc">Forma aktar</button>`;
      $("#use-calc").addEventListener("click", () => { $("#staj_gun_sayisi").value = data.count; });
    } catch (e) { out.innerHTML = '<span class="muted">Hesaplama yapılamadı.</span>'; }
  });

  /* ---------------- Google Form URL ön kontrolü (pop-up) ---------------- */
  const gfRe = /^https:\/\/(docs\.google\.com\/forms\/(u\/\d+\/)?d\/(e\/)?[A-Za-z0-9_-]{10,}(\/(viewform|edit|formResponse))?([?#].*)?|forms\.gle\/[A-Za-z0-9_-]{6,})$/;
  $$("form[data-ek16-form]").forEach((f) => {
    f.addEventListener("submit", (e) => {
      const url = $("#google_form_url", f);
      if (url && url.value.trim() && !gfRe.test(url.value.trim())) {
        e.preventDefault();
        url.classList.add("invalid");
        showModal("Geçersiz Google Form URL'si", `<p>Girdiğiniz adres geçerli bir Google Form bağlantısı değil.</p><p class="muted small">Örnek: <span class="mono">https://docs.google.com/forms/d/e/…/viewform</span> veya <span class="mono">https://forms.gle/…</span></p>`);
      }
    });
  });

  function escapeHtml(s) { return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c])); }
})();
