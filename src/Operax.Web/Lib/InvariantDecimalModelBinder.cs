using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Operax.Web.Lib;

/// <summary>
/// decimal / decimal? / double / double? / float bağlamasında tr-TR kültür tuzağını çözer.
/// HTML &lt;input type="number"&gt; değeri DAİMA nokta-ondalıklı ('12.5') gönderir; tr-TR
/// varsayılan binder noktayı binlik ayıracı sanıp '12.5'i 125 olarak okur (10x/100x para hatası).
/// Bu binder önce InvariantCulture (nokta), olmazsa tr-TR (virgül) ile dener.
/// </summary>
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
            return Task.CompletedTask; // değer yok → boş bırak (nullable null kalır)

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var raw = valueResult.FirstValue;

        var underlying = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;

        // Boş metin: nullable → null (başarı), değer tipi → bağlama yok
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (TryParse(raw, underlying, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                "Geçerli bir sayı giriniz.");
        }
        return Task.CompletedTask;
    }

    // Önce nokta (invariant — number input standardı), sonra tr-TR (kullanıcı virgülle de girebilir)
    private static bool TryParse(string raw, Type type, out object? value)
    {
        value = null;
        const NumberStyles style = NumberStyles.Number; // binlik ayıracı + ondalık + işaret
        var tr = CultureInfo.GetCultureInfo("tr-TR");

        if (type == typeof(decimal))
        {
            if (decimal.TryParse(raw, style, CultureInfo.InvariantCulture, out var d) ||
                decimal.TryParse(raw, style, tr, out d)) { value = d; return true; }
        }
        else if (type == typeof(double))
        {
            if (double.TryParse(raw, style, CultureInfo.InvariantCulture, out var d) ||
                double.TryParse(raw, style, tr, out d)) { value = d; return true; }
        }
        else if (type == typeof(float))
        {
            if (float.TryParse(raw, style, CultureInfo.InvariantCulture, out var f) ||
                float.TryParse(raw, style, tr, out f)) { value = f; return true; }
        }
        return false;
    }
}

/// <summary>decimal/double/float (ve nullable'ları) için <see cref="InvariantDecimalModelBinder"/> sağlar.</summary>
public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var t = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        if (t == typeof(decimal) || t == typeof(double) || t == typeof(float))
            return new InvariantDecimalModelBinder();
        return null;
    }
}
