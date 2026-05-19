using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSUserContext.Cmdlets.Completers;

public sealed class ScriptFileCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        IDictionary fakeBoundParameters)
    {
        return CompletionCompleters
            .CompleteFilename(wordToComplete)
            .Where(result =>
                result.ResultType == CompletionResultType.ProviderContainer ||
                IsPowerShellScript(result.CompletionText));
    }

    private static bool IsPowerShellScript(string completionText)
    {
        string text = completionText.Trim('\'', '"');

        return text.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
    }
}