// Operax Ortak Arayüz Bileşen Model'leri (View Model / DTO).
// Razor Partial'ları bu modelleri parametre olarak alır. Yeni bir partial
// eklendiğinde model'i bu dosyaya yazılır, başka yere dağıtılmaz.

using System.Collections.Generic;

namespace Operax.Web.Lib;

/// <summary>
/// Sayfa üst başlığı için breadcrumb + başlık + alt yazı + aksiyon butonları.
/// </summary>
public record PageHeaderVm(
    string Title,
    string[]? Crumbs = null,
    string? Sub = null,          // plain text — auto-encoded, güvenli
    string? SubHtml = null,      // güvenilir sunucu HTML — badge/span/tutar; kullanıcı verisi YASAK
    string? ActionsHtml = null);

/// <summary>
/// KPI kartı (Anasayfa metrik kutusu) — etiket, değer, opsiyonel birim ve trend bilgisi.
/// </summary>
public record KpiCardVm(
    string Label,
    string Value,
    string? Unit = null,
    string Glow = "brand", // brand | success | warn | danger
    string? DeltaText = null,
    string DeltaKind = "flat", // up | down | flat
    string? TrendText = null);

/// <summary>
/// Boş durum (empty state) bloğu — ikon + başlık + açıklama.
/// </summary>
public record EmptyStateVm(
    string Title,
    string? Message = null,
    string? IconSvg = null);

/// <summary>
/// Tek bir durum geçiş adımı — status timeline için.
/// </summary>
public record StatusStepVm(
    string Label,
    string State, // done | current | cancelled | pending
    string? Time = null);

/// <summary>
/// Status flow timeline (DRAFT → POSTED → CANCELLED).
/// </summary>
public record StatusFlowVm(
    IReadOnlyList<StatusStepVm> Steps);

/// <summary>
/// Tek bir sekme (tab) — etiket, query-string değeri ve opsiyonel sayı rozeti.
/// </summary>
public record TabVm(
    string Label,
    string Value,
    int?   Count = null);

/// <summary>
/// Liste sayfasındaki sekme grubu. ActiveValue mevcut seçili sekmedir.
/// QueryParamName URL'de bu sekmenin hangi parametre adıyla taşındığını söyler (ör. "Tab").
/// </summary>
public record TabsVm(
    IReadOnlyList<TabVm> Tabs,
    string ActiveValue,
    string QueryParamName = "tab");

/// <summary>
/// Belge zinciri akış butonu: sonraki belgeler, sayaçlar, gezinme linkleri.
/// _DocFlowButtons.cshtml partial'ına geçilir.
/// </summary>
/// <param name="Items">Her öğe: etiket, sayaç, link, oluştur-url (null ise sadece say)</param>
public record DocFlowVm(IReadOnlyList<DocFlowItem> Items);

/// <param name="Label">Buton etiketi (örn. "Mal Kabul")</param>
/// <param name="Count">Mevcut bağlı alt belge sayısı</param>
/// <param name="ListUrl">Mevcut alt belgelere git (null ise link yok)</param>
/// <param name="CreateUrl">Yeni alt belge oluştur URL'i (null ise sadece sayaç)</param>
/// <param name="CreateLabel">Oluştur butonu etiketi (örn. "Mal Kabul Oluştur")</param>
/// <param name="CanCreate">Bu kullanıcının oluşturma yetkisi var mı</param>
public record DocFlowItem(
    string Label,
    int Count,
    string? ListUrl = null,
    string? CreateUrl = null,
    string? CreateLabel = null,
    bool CanCreate = true);
