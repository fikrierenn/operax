// Operax — Fiyat Listesi düzenlenebilir grid (Plan 31). Tabulator tabanlı.
// Veri kaynağı: window.PL_LINES (mevcut satırlar), window.PL_ITEMS (ürün listesi),
// window.PL_CFG (priceListId, saveUrl, antiforgery token). Excel-benzeri hücre-aralığı
// kopyala/yapıştır + fill-down + tek "Kaydet" ile sunucuya bulk gönderim.
(function () {
    "use strict";
    if (typeof Tabulator === "undefined") return;

    var cfg = window.PL_CFG || {};
    var items = window.PL_ITEMS || [];

    // Ürün kimliği → "kod — ad" etiketi (formatter ve liste editörü için)
    var itemLabel = {};
    var itemValues = {};
    items.forEach(function (it) {
        itemLabel[it.id] = it.code + " — " + it.name;
        itemValues[it.id] = it.code + " — " + it.name;
    });

    // Zincir iskonto ("10+5+3") → net çarpan (ardışık, komütatif). Boş → 1.
    function netMultiplier(chain) {
        if (!chain) return 1;
        var mult = 1;
        String(chain).split("+").forEach(function (p) {
            var v = parseFloat(String(p).trim().replace(",", "."));
            if (!isNaN(v) && v > 0 && v <= 100) mult *= (1 - v / 100);
        });
        return mult;
    }

    // Net efektif fiyat hücresi (canlı önizleme — sunucu hesabıyla aynı mantık)
    function netFormatter(cell) {
        var d = cell.getData();
        var price = parseFloat(d.unitPrice) || 0;
        var net = price * netMultiplier(d.discountChain);
        return net.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    var table = new Tabulator("#pl-grid", {
        data: window.PL_LINES || [],
        layout: "fitColumns",
        height: "440px",
        placeholder: "Satır yok. 'Satır Ekle' veya 'Tüm Ürünleri Ekle' kullanın.",
        reactiveData: true,
        clipboard: true,
        clipboardPasteAction: "range",
        selectableRange: true,
        selectableRangeColumns: true,
        selectableRangeRows: true,
        editTriggerEvent: "click",
        columns: [
            {
                title: "Ürün", field: "itemId", widthGrow: 3, minWidth: 200, editor: "list",
                editorParams: { values: itemValues, autocomplete: true, listOnEmpty: true },
                formatter: function (cell) { return itemLabel[cell.getValue()] || "<span class='text-red-600'>Seçiniz…</span>"; }
            },
            {
                title: "Brüt Fiyat", field: "unitPrice", width: 110, hozAlign: "right",
                editor: "number", editorParams: { min: 0, step: 0.0001 },
                formatter: "money", formatterParams: { decimal: ",", thousand: ".", precision: 2 }
            },
            {
                title: "Min Miktar", field: "minQty", width: 95, hozAlign: "right",
                editor: "number", editorParams: { min: 0, step: 0.000001 }
            },
            {
                title: "Tip", field: "lineType", width: 95,
                editor: "list", editorParams: { values: { "FIXED": "Sabit", "DISCOUNT": "İskonto" } },
                formatter: function (cell) { return cell.getValue() === "DISCOUNT" ? "İskonto" : "Sabit"; }
            },
            {
                title: "İskonto Zinciri", field: "discountChain", width: 125,
                editor: "input", editorParams: { placeholder: "10+5+3" }
            },
            {
                title: "Net Efektif", field: "net", width: 115, hozAlign: "right",
                headerSort: false, formatter: netFormatter
            },
            {
                title: "", field: "_del", width: 44, hozAlign: "center", headerSort: false,
                formatter: function () { return "<span title='Sil' style='cursor:pointer'>🗑</span>"; },
                cellClick: function (e, cell) { cell.getRow().delete(); }
            }
        ]
    });

    // Yeni boş satır ekle (varsayılan tip sabit)
    var addBtn = document.getElementById("pl-add-row");
    if (addBtn) addBtn.addEventListener("click", function () {
        table.addRow({ itemId: "", unitPrice: 0, minQty: 0, lineType: "FIXED", discountChain: "" });
    });

    // Fill-down (Ctrl+D): seçili aralığın ilk satır değerini alta kopyala
    document.addEventListener("keydown", function (e) {
        if (!(e.ctrlKey && (e.key === "d" || e.key === "D"))) return;
        var ranges = table.getRanges ? table.getRanges() : [];
        if (!ranges.length) return;
        e.preventDefault();
        ranges.forEach(function (r) {
            var rows = r.getRows(), cols = r.getColumns();
            if (rows.length < 2) return;
            var first = rows[0].getData();
            for (var i = 1; i < rows.length; i++) {
                cols.forEach(function (c) {
                    var f = c.getField();
                    if (f && f !== "net" && f !== "_del") rows[i].update(makeUpdate(f, first[f]));
                });
            }
        });
    });
    function makeUpdate(field, val) { var o = {}; o[field] = val; return o; }

    // Kaydet: satırları topla, sunucuya bulk gönder (SyncDelete=1 → grid otoritedir)
    var saveBtn = document.getElementById("pl-save");
    if (saveBtn) saveBtn.addEventListener("click", function () {
        var rows = table.getData()
            .filter(function (r) { return r.itemId; })
            .map(function (r, i) {
                return {
                    rowNo: i + 1,
                    itemId: r.itemId,
                    unitPrice: parseFloat(r.unitPrice) || 0,
                    minQty: parseFloat(r.minQty) || 0,
                    lineType: r.lineType || "FIXED",
                    discountChain: r.discountChain || null
                };
            });
        // Kazara veri kaybı koruması: boş grid kaydı tüm satırları siler (SyncDelete=1)
        if (rows.length === 0 &&
            !confirm("Listede satır yok. Kaydedilirse mevcut TÜM satırlar silinecek. Devam edilsin mi?")) {
            return;
        }
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
