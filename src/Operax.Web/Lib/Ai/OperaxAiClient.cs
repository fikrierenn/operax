using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;

namespace Operax.Web.Lib.Ai;

/// <summary>
/// Yerel LLM (qwen2.5) için minimal istemci — LLamaSharp ile SÜREÇ-İÇİ (in-process).
/// Bulut/HTTP yok; model .gguf uygulamaya gömülü. Soft-fail: model yoksa/hata olursa
/// iş akışı bloke olmaz, verdict UNCHECKED döner. Singleton (model bir kez yüklenir).
/// </summary>
public interface IOperaxAiClient
{
    /// <summary>Bir override gerekçesinin anlamlı/makul olup olmadığını yerel AI ile denetler.</summary>
    Task<AiReasonVerdict> CheckJustificationAsync(string context, string reason, CancellationToken ct = default);
}

/// <summary>AI gerekçe değerlendirme sonucu. Verdict: PLAUSIBLE | IMPLAUSIBLE | UNCHECKED.</summary>
public sealed record AiReasonVerdict(string Verdict, string? Comment)
{
    public const string Plausible   = "PLAUSIBLE";
    public const string Implausible = "IMPLAUSIBLE";
    public const string Unchecked   = "UNCHECKED";

    public static AiReasonVerdict NotChecked(string? why = null) => new(Unchecked, why);
}

/// <summary>appsettings "Ai" bölümü — yerel LLM yapılandırması.</summary>
public sealed class AiOptions
{
    public bool   Enabled        { get; set; } = false;
    public string ModelFileName  { get; set; } = "qwen25-3b-instruct-q4_k_m.gguf";
    public int    ContextSize    { get; set; } = 4096;
    public int    MaxTokens      { get; set; } = 300;
}

public sealed class OperaxAiClient : IOperaxAiClient, IDisposable
{
    private readonly ILogger<OperaxAiClient> _logger;
    private readonly AiOptions _opt;
    private readonly string _modelPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Model tembel yüklenir (ilk kullanımda); Ai kapalı/dosya yoksa hiç yüklenmez.
    private LLamaWeights? _weights;
    private ModelParams? _modelParams;
    private bool _loadAttempted;

    public OperaxAiClient(IConfiguration config, IWebHostEnvironment env, ILogger<OperaxAiClient> logger)
    {
        _logger = logger;
        _opt = new AiOptions();
        config.GetSection("Ai").Bind(_opt);
        _modelPath = Path.Combine(env.ContentRootPath, "App_Data", "models", "llm", _opt.ModelFileName);
    }

    public async Task<AiReasonVerdict> CheckJustificationAsync(string context, string reason, CancellationToken ct = default)
    {
        // AI kapalı veya gerekçe boş → denetleme yapma (soft)
        if (!_opt.Enabled) return AiReasonVerdict.NotChecked("AI devre dışı");
        if (string.IsNullOrWhiteSpace(reason)) return AiReasonVerdict.NotChecked("Gerekçe boş");

        try
        {
            return await RunInferenceAsync(context, reason, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // gerçek iptal — üst katmana
        }
        catch (Exception ex)
        {
            // Soft-fail: yerel AI hata verirse iş akışı bloke olmaz
            _logger.LogWarning(ex, "Yerel AI gerekçe denetimi başarısız — UNCHECKED dönülüyor.");
            return AiReasonVerdict.NotChecked("AI erişilemedi");
        }
    }

    private async Task<AiReasonVerdict> RunInferenceAsync(string context, string reason, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            EnsureModelLoaded();
            if (_weights is null || _modelParams is null)
                return AiReasonVerdict.NotChecked("Model yüklenemedi");

            const string system =
                "Sen bir satınalma denetçisisin. Kullanıcı bir fiyat farkı için gerekçe yazdı. " +
                "Gerekçenin İŞ AÇISINDAN ANLAMLI ve fiyat farkını AÇIKLAYAN bir metin olup olmadığını değerlendir. " +
                "Anlamsız, boş, alakasız, rastgele karakter veya gerekçe niteliği taşımayan metinler IMPLAUSIBLE'dır. " +
                "Yalnızca şu JSON formatında yanıt ver: " +
                "{\"verdict\":\"PLAUSIBLE\",\"comment\":\"tek cümle Türkçe değerlendirme\"} " +
                "veya {\"verdict\":\"IMPLAUSIBLE\",\"comment\":\"...\"}";

            var prompt = BuildQwenPrompt(system, $"Bağlam: {context}\nGerekçe: {reason}");

            var executor = new StatelessExecutor(_weights, _modelParams);
            var inferenceParams = new InferenceParams
            {
                MaxTokens   = _opt.MaxTokens,
                AntiPrompts = ["<|im_end|>", "<|im_start|>"]
            };

            var buffer = new StringBuilder();
            await foreach (var token in executor.InferAsync(prompt, inferenceParams, ct))
            {
                buffer.Append(token);
                if (ct.IsCancellationRequested) break;
            }

            return ParseVerdict(StripAntiPrompts(buffer.ToString()).Trim());
        }
        finally
        {
            _gate.Release();
        }
    }

    // Model ilk kullanımda yüklenir (2GB mmap). Bir kez denenir; başarısızsa tekrar denenmez.
    private void EnsureModelLoaded()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;

        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning("Yerel AI model dosyası yok, AI devre dışı: {Path}", _modelPath);
            return;
        }
        try
        {
            _modelParams = new ModelParams(_modelPath) { ContextSize = (uint)_opt.ContextSize, GpuLayerCount = 0 };
            _weights = LLamaWeights.LoadFromFile(_modelParams);
            _logger.LogInformation("Yerel AI hazır: {Model} (ctx={Ctx})", _opt.ModelFileName, _opt.ContextSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yerel AI model yükleme hatası — AI devre dışı.");
            _weights = null;
        }
    }

    // qwen2.5 sohbet şablonu
    private static string BuildQwenPrompt(string system, string user) =>
        $"<|im_start|>system\n{system}<|im_end|>\n" +
        $"<|im_start|>user\n{user}<|im_end|>\n" +
        "<|im_start|>assistant\n";

    private static string StripAntiPrompts(string output)
    {
        int idx = output.IndexOf("<|im_end|>", StringComparison.Ordinal);
        if (idx >= 0) output = output[..idx];
        idx = output.IndexOf("<|im_start|>", StringComparison.Ordinal);
        if (idx >= 0) output = output[..idx];
        return output;
    }

    // Model çıktısındaki JSON'u ayrıştır; beklenmeyen değer → UNCHECKED (güvenli taraf)
    private AiReasonVerdict ParseVerdict(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return AiReasonVerdict.NotChecked("AI boş yanıt");

        // Model bazen JSON dışı metin ekler — ilk { ... } bloğunu çek
        int s = raw.IndexOf('{'), e = raw.LastIndexOf('}');
        if (s < 0 || e <= s) return AiReasonVerdict.NotChecked("AI JSON üretmedi");

        try
        {
            using var doc = JsonDocument.Parse(raw[s..(e + 1)]);
            var verdict = doc.RootElement.TryGetProperty("verdict", out var v) ? v.GetString() : null;
            var comment = doc.RootElement.TryGetProperty("comment", out var c) ? c.GetString() : null;

            verdict = verdict?.ToUpperInvariant() switch
            {
                AiReasonVerdict.Plausible   => AiReasonVerdict.Plausible,
                AiReasonVerdict.Implausible => AiReasonVerdict.Implausible,
                _ => AiReasonVerdict.Unchecked
            };
            return new AiReasonVerdict(verdict, Truncate(comment, 500));
        }
        catch (JsonException)
        {
            return AiReasonVerdict.NotChecked("AI yanıtı çözümlenemedi");
        }
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

    public void Dispose()
    {
        _weights?.Dispose();
        _gate.Dispose();
    }
}
