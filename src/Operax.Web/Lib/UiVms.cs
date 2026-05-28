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
    string? Sub = null,
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
