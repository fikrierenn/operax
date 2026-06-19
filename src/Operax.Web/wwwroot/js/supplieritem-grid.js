// Operax — Tedarikçi-ürün kataloğu düzenlenebilir grid (Plan 32). Tabulator tabanlı.
// Veri: window.SI_PARTNERS (tedarikçi listesi), window.SI_LINES (mevcut), window.SI_CFG
// (itemId, saveUrl, token). Excel-benzeri kopyala/yapıştır + tek "Kaydet" bulk gönderim.
// IsPreferred tek-seçim: bir tedarikçi tercih yapılınca diğerleri otomatik düşer.
(function () {
    "use strict";
    if (typeof Tabulator === "undefined") return;

    var cfg = window.SI_CFG || {};
    var partners = window.SI_PARTNERS || [];

    // Tedarikçi kimliği → "kod — ad" (formatter + list editor)
    var partnerLabel = {}, partnerValues = {};
    partners.forEach(function (p) {
        var lbl = (p.code ? p.code + " — " : "") + p.name;
        partnerLabel[p.id] = lbl;
        partnerValues[p.id] = lbl;
    });

    var table = new Tabulator("#si-grid", {
        data: window.SI_LINES || [],
        layout: "fitColumns",
        height: "420px",
        placeholder: "Tedarikçi yok. 'Tedarikçi Ekle' ile satır açın.",
        reactiveData: true,
        clipboard: true,
        clipboardPasteAction: "range",
        selectableRange: true,
        selectableRangeColumns: true,
        selectableRangeRows: true,
        editTriggerEvent: "click",
        // Tek-tercih kuralı: bir satır preferred yapılınca diğerlerini düşür
        cellEdited: function (cell) {
            if (cell.getField() === "isPreferred" && cell.getValue() === true) {
                table.getRows().forEach(function (r) {
                    if (r !== cell.getRow() && r.getData().isPreferred) r.update({ isPreferred: false });
                });
            }
        },
        columns: [
            {
                title: "Tedarikçi", field: "partnerId", widthGrow: 3, minWidth: 200, editor: "list",
                editorParams: { values: partnerValues, autocomplete: true, listOnEmpty: true },
                formatter: function (cell) { return partnerLabel[cell.getValue()] || "<span class='text-red-600'>Seçiniz…</span>"; }
            },
            { title: "Tedarikçi Ürün Kodu", field: "supplierItemCode", width: 160, editor: "input" },
            { title: "Tedarikçi Ürün Adı", field: "supplierItemName", widthGrow: 2, minWidth: 150, editor: "input" },
            {
                title: "Teslim (gün)", field: "leadTimeDays", width: 110, hozAlign: "right",
                editor: "number", editorParams: { min: 0 }
            },
            {
                title: "Min Sipariş", field: "minOrderQty", width: 110, hozAlign: "right",
                editor: "number", editorParams: { min: 0, step: 0.000001 }
            },
            {
                title: "Son Fiyat", field: "lastPrice", width: 110, hozAlign: "right",
                editor: "number", editorParams: { min: 0, step: 0.0001 },
                formatter: "money", formatterParams: { decimal: ",", thousand: ".", precision: 2 }
            },
            {
                title: "Tercih", field: "isPreferred", width: 80, hozAlign: "center",
                editor: "tickCross", formatter: "tickCross",
                formatterParams: { allowEmpty: true, crossElement: "" }
            },
            {
                title: "", field: "_del", width: 44, hozAlign: "center", headerSort: false,
                formatter: function () { return "<span title='Sil' style='cursor:pointer'>🗑</span>"; },
                cellClick: function (e, cell) { cell.getRow().delete(); }
            }
        ]
    });

    var addBtn = document.getElementById("si-add-row");
    if (addBtn) addBtn.addEventListener("click", function () {
        table.addRow({ partnerId: "", supplierItemCode: "", supplierItemName: "", leadTimeDays: null, minOrderQty: 0, lastPrice: null, currency: "TRY", isPreferred: false });
    });

    var saveBtn = document.getElementById("si-save");
    if (saveBtn) saveBtn.addEventListener("click", function () {
        var rows = table.getData()
            .filter(function (r) { return r.partnerId; })
            .map(function (r, i) {
                return {
                    rowNo: i + 1,
                    partnerId: r.partnerId,
                    supplierItemCode: r.supplierItemCode || null,
                    supplierItemName: r.supplierItemName || null,
                    leadTimeDays: (r.leadTimeDays === "" || r.leadTimeDays == null) ? null : parseInt(r.leadTimeDays, 10),
                    minOrderQty: parseFloat(r.minOrderQty) || 0,
                    lastPrice: (r.lastPrice === "" || r.lastPrice == null) ? null : parseFloat(r.lastPrice),
                    currency: r.currency || "TRY",
                    isPreferred: r.isPreferred === true
                };
            });
        saveBtn.disabled = true;
        saveBtn.textContent = "Kaydediliyor…";
        fetch(cfg.saveUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": cfg.token },
            body: JSON.stringify(rows)
        }).then(function (res) { return res.json().catch(function () { return { ok: false, error: "Sunucu yanıtı okunamadı." }; }); })
          .then(function (data) {
              if (data && data.ok) { window.location.reload(); }
              else { alert((data && data.error) || "Kaydetme başarısız."); saveBtn.disabled = false; saveBtn.textContent = "Kaydet"; }
          })
          .catch(function () { alert("Ağ hatası — kaydedilemedi."); saveBtn.disabled = false; saveBtn.textContent = "Kaydet"; });
    });
})();
