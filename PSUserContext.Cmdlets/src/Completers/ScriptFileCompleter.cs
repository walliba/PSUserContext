using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSUserContext.Cmdlets.Completers;

public class ScriptFileCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string      commandName,
        string      parameterName,
        string      wordToComplete,
        CommandAst  commandAst,
        IDictionary fakeBoundParameters)
    {
        string? dir = Path.GetDirectoryName(wordToComplete);

        if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;

        string pattern = Path.GetFileName(wordToComplete) + "*.ps1";

        return Directory.EnumerateFiles(dir, pattern)
            .Select(f => new CompletionResult(f))
            .ToArray();
    }
}