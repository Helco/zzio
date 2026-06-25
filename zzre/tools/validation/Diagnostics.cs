using System;

namespace zzre;

public static partial class Diagnostics
{
    public static readonly DiagnosticCategory CategoryValidation = new("VAL");

    public static readonly DiagnosticType TypeValIgnoredDueToExtension =
        CategoryValidation.Information("Ignored file due to extension: {0}");
    public static Diagnostic ValIgnoredDueToExtension(string file, string ext) =>
        TypeValIgnoredDueToExtension.Create([ext], [new(file)]);

    public static readonly DiagnosticType TypeValGeneralException =
        CategoryValidation.Error("Exception during validation: {0}", footNote: "Stack trace: \n {1}");
    public static Diagnostic ValGeneralException(string file, Exception e) =>
        TypeValGeneralException.Create([e.Message, e.StackTrace ?? ""], [new(file)]);
}
