---
name: local-llm-integration
description: Operax'ta yerel LLM (LLamaSharp süreç-içi, GGUF) çağırma standardı. "yerel LLM", "LLamaSharp", "local inference", "LLM çağır", "AI gerekçe/özet/değerlendirme" denildiğinde veya LLama. namespace içeren kod yazarken danış. Doğru executor, chat template, qwen3 no_think, sampling, soft-fail, 0-token sorun giderme.
allowed-tools: Read, Grep, Glob, Edit, Bash
user-invocable: true
model: inherit
---

# Operax Yerel LLM Entegrasyonu (LLamaSharp 0.27)

Bulut/HTTP yok — model GGUF olarak süreç-içi (in-process) yüklenir. Veri makineden çıkmaz.
İlişkili: `src/Operax.Web/Lib/Ai/OperaxAiClient.cs` (referans uygulama).

## 1. Paket Pin (ZORUNLU eşitlik)

```xml
<PackageReference Include="LLamaSharp" Version="0.27.0" />
<PackageReference Include="LLamaSharp.Backend.Cpu" Version="0.27.0" />
```
**Managed ve Backend versiyonu HER ZAMAN eşit.** Farklıysa native yüklenir ama sessizce 0 token döner.

## 2. Model Dosyası

- Konum: `src/Operax.Web/App_Data/models/llm/` (`.gitignore`'da — 2GB+ git dışı).
- **Önerilen: `Qwen3-4B-Q4_K_M.gguf`** (~2.5 GB, 16GB RAM rahat). Kaliteli istenirse `Qwen3-8B-Q4_K_M` (~5 GB, 32GB RAM).
- **DENSE Qwen3 kullan** — Qwen3-Next / Qwen3.x hibrit MoE+DeltaNet modelleri llama.cpp'de 0-token + context bug'ı yaşar (native logda "fused Gated Delta Net" görürsen model YANLIŞ).
- İndirme: `curl -L -o <path> "https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/main/Qwen3-4B-Q4_K_M.gguf"`

## 3. Çalışan Pattern (StatelessExecutor + ApplyTemplate)

Tek-seferlik JSON verdict gibi durumsuz işler için `StatelessExecutor`. **Chat template'i ELLE kurma** — `ApplyTemplate=true` + `SystemMessage` ile LLamaSharp uygular (elle `<|im_start|>` yazmak çift-BOS / 0-token riski).

```csharp
var p = new ModelParams(modelPath) { ContextSize = 4096, GpuLayerCount = 0, BatchSize = 128 };
using var model = await LLamaWeights.LoadFromFileAsync(p);
var executor = new StatelessExecutor(model, p)
{
    ApplyTemplate = true,
    SystemMessage = "Sen bir ... denetçisisin. Yalnızca JSON döndür. /no_think"
};
var ip = new InferenceParams
{
    SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.2f, TopP = 0.95f, TopK = 20 },
    MaxTokens = 300,
    AntiPrompts = ["<|im_end|>"]   // MİNİMAL — kısa string ("\n", ".") erken eşleşip 0-token yapar
};
var sb = new StringBuilder();
await foreach (var t in executor.InferAsync(userPrompt + " /no_think", ip, ct)) sb.Append(t);
var clean = System.Text.RegularExpressions.Regex.Replace(
    sb.ToString(), @"<think>[\s\S]*?</think>", "").Trim();
```

## 4. Qwen3 Thinking Mode Tuzağı

Qwen3 varsayılan `<think>...</think>` üretir → JSON parse'ı bozar.
- **`/no_think`** prompt/SystemMessage sonuna ekle (en güvenilir).
- Yine de gelirse `<think>` bloğunu regex ile temizle (yukarıda).
- `enable_thinking=false` chat-template-kwargs llama.cpp bug'ı — GÜVENME.

## 5. Soft-Fail (ZORUNLU)

Yerel LLM iş akışını ASLA bloke etmez. Model yok / 0 token / hata → fallback (örn. `UNCHECKED`), iş devam eder.
```csharp
if (!_opt.Enabled) return Fallback("AI devre dışı");
try { var r = await Run(...); return string.IsNullOrWhiteSpace(r) ? Fallback("boş yanıt") : Parse(r); }
catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
catch (Exception ex) { _logger.LogWarning(ex, "Yerel LLM başarısız"); return Fallback("erişilemedi"); }
```
- Model singleton + tembel yükleme (2GB; `Ai:Enabled=false` ise hiç yükleme).
- `SemaphoreSlim(1,1)` ile tek-seferde tek inference (RAM koruması).

## 6. 0-Token Sorun Giderme Checklist

1. `LLamaSharp` == `LLamaSharp.Backend.Cpu` versiyonu mu?
2. `SamplingPipeline` atandı mı (null bırakma)?
3. `AntiPrompts` çok kısa string içeriyor mu (`"\n"`, `"."` → erken durur)?
4. `ApplyTemplate=true` + `SystemMessage` var mı (elle template kurmuyor musun)?
5. Native logda "fused Gated Delta Net" → model hibrit/yanlış → Dense Qwen3-4B/8B.
6. `MaxTokens > 0` mu? `GpuLayerCount=0` (saf CPU) mu?
7. Elle BOS/`<|im_start|>` ekleyip çift-BOS yapıyor musun?

## Kaynaklar
- SciSharp/LLamaSharp `LLama.Examples/Examples/StatelessModeExecute.cs`
- LLamaSharp Issue #1056 (AntiPrompts), #1021 (chat template)
- Qwen/Qwen3-4B-GGUF · Qwen/Qwen3-8B-GGUF (HuggingFace)
- qwen.readthedocs.io llama.cpp rehberi
