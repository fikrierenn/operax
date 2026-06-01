# Operax — Ekran Envanteri (Yardım Butonu + Kullanıcı Kitapçığı Kaynağı)

> 2026-06-02 envanteri. 95 Razor sayfası (partial hariç). Her ekrana yardım butonu + kitapçık bölümü bu listeden üretilir.
> Yardım butonu enjeksiyon noktası: `Features/Shared/_PageHeader.cshtml` (`ActionsHtml`, sağ üst) → `PageHeaderVm.ScreenId` + `/Help/{screenId}` Markdown.
> Kitapçık: `docs/manual/<modül>.md` — ekran-içi yardım ile TEK kaynak.

## M00 — Admin & Yönetim (16)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Kullanıcı Listesi | /Admin/Users/Index | Sistem kullanıcılarını yönet | Admin/Users/Index.cshtml |
| Yeni Kullanıcı | /Admin/Users/Create | Yeni kullanıcı oluştur | Admin/Users/Create.cshtml |
| Kullanıcı Düzenle | /Admin/Users/Edit/{id} | Kullanıcı bilgisi düzenle | Admin/Users/Edit.cshtml |
| Yetki Grupları | /Admin/Roles/Index | Rol tabanlı erişim | Admin/Roles/Index.cshtml |
| Yeni Yetki Grubu | /Admin/Roles/Create | Yeni rol tanımla | Admin/Roles/Create.cshtml |
| Modül İzinleri | /Admin/Roles/Permissions/{id} | Rol modül erişim ayarı | Admin/Roles/Permissions.cshtml |
| Sistem Ayarları | /Admin/Settings/Index | Global parametreler | Admin/Settings/Index.cshtml |
| Denetim Kayıtları | /Admin/AuditLog/Index | İşlem geçmişi | Admin/AuditLog/Index.cshtml |
| Sözlük Yönetimi | /Admin/Dictionary/Index | Sistem sabitleri | Admin/Dictionary/Index.cshtml |
| Sözlük Detay | /Admin/Dictionary/Details/{id} | Sözlük tanımı düzenle | Admin/Dictionary/Details.cshtml |
| Sözlük Değerleri | /Admin/Dictionary/Values/{typeId} | Sözlük değer listesi | Admin/Dictionary/Values.cshtml |
| Belge Serileri | /Admin/NumberSeries/Index | Evrak numaralandırma | Admin/NumberSeries/Index.cshtml |
| Sistem Parametreleri | /Admin/Parameters/Index | Ayarlanabilir parametreler | Admin/Parameters/Index.cshtml |
| Statü Geçişleri | /Admin/StatusTransitions/Index | Evrak durum kuralları | Admin/StatusTransitions/Index.cshtml |
| Modül Aktivasyonu | /Admin/Modules/Index | Modül aç/kapa | Admin/Modules/Index.cshtml |

## Auth — Kimlik (3)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Giriş | /login | Oturum açma | Auth/Login.cshtml |
| Çıkış | /Logout | Oturum kapatma | Auth/Logout.cshtml |
| Yetkiniz Yok | /Auth/AccessDenied | Erişim yetkisiz | Auth/AccessDenied.cshtml |

## M01 — Dashboard (1)
| Anasayfa | /Dashboard/Index | KPI + özet grafik | Dashboard/Index.cshtml |

## M01 — Master Data (11)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Ürün Listesi | /MasterData/Items/Index | Katalog ürünleri | MasterData/Items/Index.cshtml |
| Ürün Detay/Yeni | /MasterData/Items/Details/{id?} | Ürün kartı | MasterData/Items/Details.cshtml |
| Cari Listesi | /MasterData/Partners/Index | Müşteri/Tedarikçi | MasterData/Partners/Index.cshtml |
| Cari Detay/Yeni | /MasterData/Partners/Details/{id?} | Cari kartı + Fatura/Çek/Ekstre tab | MasterData/Partners/Details.cshtml |
| Depolar & Raflar | /Warehouses/Index | Depo + hücre yapısı | Warehouses/Index.cshtml |
| Depo Detay/Yeni | /Warehouses/Details/{id?} | Depo + hücre listesi | Warehouses/Details.cshtml |
| Lokasyonlar | /MasterData/Locations/Index | Hücre adresleri (WMS) | MasterData/Locations/Index.cshtml |
| Şubeler | /MasterData/Branches/Index | Organizasyon şubeleri | MasterData/Branches/Index.cshtml |
| Şube Detay/Yeni | /MasterData/Branches/Details/{id?} | Şube tanımı | MasterData/Branches/Details.cshtml |

## M02 — Stok (8)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Stok Bakiyesi | /Inventory/Balance/Index | Anlık miktar (depo/bin) | Inventory/Balance/Index.cshtml |
| Stok Hareketleri | /Inventory/Movements/Index | Giriş/çıkış/transfer | Inventory/Movements/Index.cshtml |
| Lot/Parti | /Lot/Index | Üretim partileri | Lot/Index.cshtml |
| Lot Detay | /Lot/Details/{id} | Parti detay | Lot/Details.cshtml |
| Seri No | /Serial/Index | Seri no tarayıcı | Serial/Index.cshtml |
| Seri Detay | /Serial/Details/{id} | Seri no detay | Serial/Details.cshtml |
| Palet (LPN) | /LPN/Index | Lojistik paletler | LPN/Index.cshtml |
| Palet Detay | /LPN/Details/{code} | Palet içerik/durum | LPN/Details.cshtml |

## M03 — Satınalma (8)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Sipariş Listesi | /PurchaseOrders/Index | Satınalma siparişleri | PurchaseOrders/Index.cshtml |
| Sipariş Detay/Yeni | /PurchaseOrders/Details/{id?} | Sipariş oluştur/onayla | PurchaseOrders/Details.cshtml |
| Fiyat Farkları | /PurchaseOrders/PriceVariances | PO↔Fatura fiyat farkı | PurchaseOrders/PriceVariances.cshtml |
| Mal Kabul | /Receiving/Index | Mal kabul listesi | Receiving/Index.cshtml |
| Mal Kabul Detay/Yeni | /Receiving/Details/{id?} | Gelen mal kaydı + yerleme | Receiving/Details.cshtml |
| Mal Kabul Terminali | /Receiving/Terminal | El terminali barkod | Receiving/Terminal.cshtml |
| Alış Faturaları | /PurchaseInvoices/Index | Tedarikçi faturaları | PurchaseInvoices/Index.cshtml |
| Alış Faturası Detay | /PurchaseInvoices/Details | Fatura + ödeme planı | PurchaseInvoices/Details.cshtml |

## M04 — Satış (8)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Sipariş Listesi | /SalesOrders/Index | Müşteri siparişleri | SalesOrders/Index.cshtml |
| Sipariş Detay/Yeni | /SalesOrders/Details/{id?} | Sipariş oluştur/onayla | SalesOrders/Details.cshtml |
| Sevkiyat Listesi | /Shipping/Index | Sevkiyat yönetimi | Shipping/Index.cshtml |
| Sevkiyat Detay/Yeni | /Shipping/Details/{id?} | Sevkiyat oluştur/onayla | Shipping/Details.cshtml |
| Sevkiyat Terminali | /Shipping/Terminal | El terminali koli doğrulama | Shipping/Terminal.cshtml |
| Satış Faturaları | /SalesInvoices/Index | Müşteri faturaları | SalesInvoices/Index.cshtml |
| Satış Faturası Yeni | /SalesInvoices/Create | Sevkiyattan fatura (N:1) | SalesInvoices/Create.cshtml |
| Satış Faturası Detay | /SalesInvoices/Details | Fatura detay | SalesInvoices/Details.cshtml |

## M08 — Sayım & Transfer (8)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Döngüsel Sayım | /CycleCount/Index | Periyodik stok kontrol | CycleCount/Index.cshtml |
| Sayım Detay/Yeni | /CycleCount/Details/{id?} | Sayım oturumu | CycleCount/Details.cshtml |
| Sayım Terminali | /CycleCount/Terminal | El terminali miktar | CycleCount/Terminal.cshtml |
| Transferler | /Transfer/Index | Depo içi hareket | Transfer/Index.cshtml |
| Transfer Detay/Yeni | /Transfer/Details/{id?} | Transfer komutu | Transfer/Details.cshtml |
| Transfer Terminali | /Transfer/Terminal | El terminali transfer | Transfer/Terminal.cshtml |
| Putaway Wizard | /Transfer/Putaway | Kabulden hücreye yerleştir | Transfer/Putaway.cshtml |
| Replenishment Wizard | /Transfer/Replenishment | Toplama rafı besle | Transfer/Replenishment.cshtml |

## M10 — Toplama/Picking (3)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Toplama Görevleri | /Picking/Index | Picking listesi | Picking/Index.cshtml |
| Toplama Detay | /Picking/Details/{id} | Toplama görevi | Picking/Details.cshtml |
| Toplama Terminali | /Picking/Terminal | El terminali toplama | Picking/Terminal.cshtml |

## M11 — Finans (16)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Finansal Hesaplar | /Finance/Accounts/Index | Muhasebe hesapları | Finance/Accounts/Index.cshtml |
| Hesap Yeni | /Finance/Accounts/Create | Hesap tanımı | Finance/Accounts/Create.cshtml |
| Hesap Detay | /Finance/Accounts/Details | Hesap ekstre/bakiye | Finance/Accounts/Details.cshtml |
| Çekler | /Finance/Cheques/Index | Çek/senet portföyü | Finance/Cheques/Index.cshtml |
| Çek Yeni | /Finance/Cheques/Create | Çek kaydı | Finance/Cheques/Create.cshtml |
| Çek Detay | /Finance/Cheques/Details | Çek durum geçişi | Finance/Cheques/Details.cshtml |
| Kredi Kartları | /Finance/CreditCards/Index | Kayıtlı kartlar | Finance/CreditCards/Index.cshtml |
| Kredi Kartı Yeni | /Finance/CreditCards/Create | Kart tanımı | Finance/CreditCards/Create.cshtml |
| Kredi Kartı Detay | /Finance/CreditCards/Details | Kart ekstre/hareket | Finance/CreditCards/Details.cshtml |
| Krediler | /Finance/Loans/Index | Banka kredileri | Finance/Loans/Index.cshtml |
| Kredi Yeni | /Finance/Loans/Create | Kredi tanımı | Finance/Loans/Create.cshtml |
| Kredi Detay | /Finance/Loans/Details | Taksit/ödeme | Finance/Loans/Details.cshtml |
| Ödeme Planı | /Finance/PaymentPlan/Index | Ödeme takvimi | Finance/PaymentPlan/Index.cshtml |
| Ödeme/Tahsilat Girişi | /Finance/Payments/Create | Ödeme/tahsilat | Finance/Payments/Create.cshtml |
| Yaşlandırma | /Finance/Aging/Index | Alacak/borç yaşlandırma | Finance/Aging/Index.cshtml |
| Yaşlandırma Detay | /Finance/Aging/Details | Cari bazında yaşlandırma | Finance/Aging/Details.cshtml |
| Mali Durum Kapama | /Finance/Snapshot/Index | Dönem sonu özet | Finance/Snapshot/Index.cshtml |

## M12 — Üretim (7)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Reçeteler (BOM) | /Manufacturing/BOM/Index | Üretim formülleri | Manufacturing/BOM/Index.cshtml |
| BOM Detay/Yeni | /Manufacturing/BOM/Details/{id?} | BOM + malzemeler | Manufacturing/BOM/Details.cshtml |
| İş Merkezleri | /Manufacturing/WorkCenters/Index | İstasyon/makine | Manufacturing/WorkCenters/Index.cshtml |
| Üretim Rotaları | /Manufacturing/WorkOrders/Index | BOM'den rota | Manufacturing/WorkOrders/Index.cshtml |
| Rota Detay | /Manufacturing/WorkOrders/Details/{id} | Rota adımları | Manufacturing/WorkOrders/Details.cshtml |
| Üretim Emirleri | /Production/Index | Üretim işleri | Production/Index.cshtml |
| Üretim Emri Detay | /Production/Details/{id} | İş emri + sarf | Production/Details.cshtml |
| Üretim Terminali | /Production/Terminal | El terminali üretim | Production/Terminal.cshtml |

## M13 — Gider (3)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Gider Faturaları | /Expenses/Index | İç gider kayıtları | Expenses/Index.cshtml |
| Gider Detay/Yeni | /Expenses/Details/{id?} | Gider kaydı | Expenses/Details.cshtml |
| Gider Raporu | /Expenses/Report/Index | Maliyet merkezi özet | Expenses/Report/Index.cshtml |

## M14 — Bütçe (2)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Bütçe Yönetimi | /Budget/Index | Merkez bütçeleri | Budget/Index.cshtml |
| Bütçe Detay/Yeni | /Budget/Details/{id?} | Bütçe + dönem tahsisat | Budget/Details.cshtml |

## M16 — Sarf (2)
| Ekran | Route | Amaç | Dosya |
|---|---|---|---|
| Sarf Fişleri | /MaterialIssue/Index | İç malzeme çıkışı | MaterialIssue/Index.cshtml |
| Sarf Detay/Yeni | /MaterialIssue/Details/{id?} | Sarf kaydı | MaterialIssue/Details.cshtml |

---

**TOPLAM: 95 ekran / 14 modül.** Kitapçık + yardım butonu işi bu envanterden ajanlarla toplu üretilecek (modül başına 1 ajan → ekran amacı + adım-adım kullanım).
