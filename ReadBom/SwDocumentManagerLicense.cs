namespace ReadBom;

internal static class SwDocumentManagerLicense
{
    private const string EmbeddedLicense = "SOLIDWORKS_2024:swdocmgr_general-11785-02051-00064-17409-06675-36864-49921-00003-16444-17292-40706-13647-03990-41452-61997-10243-50029-57881-52744-28545-12440-55304-12042-40614-37528-44680-42142-42646-25790-25696-00104-13084-11568-30049-11623-12338-12850-7,swdocmgr_previews-11785-02051-00064-17409-06675-36864-49921-00003-08620-59331-29458-22871-23495-58620-31231-20480-40804-52174-44857-61165-00436-50636-11406-40614-37528-44680-42142-42646-25790-25696-00104-13084-11568-30049-11623-12338-12850-2,swdocmgr_geometry-11785-02051-00064-17409-06675-36864-49921-00003-32176-18373-30431-43285-23919-57556-33875-41987-56383-14305-39004-32829-13276-07414-11639-40614-37528-44680-42142-42646-25790-25696-00104-13084-11568-30049-11623-12338-12850-6,swdocmgr_dimxpert-11785-02051-00064-17409-06675-36864-49921-00003-46144-01140-54586-63408-63476-48084-22850-51200-60093-50547-28541-15939-14471-33333-12150-40614-37528-44680-42142-42646-25790-25696-00104-13084-11568-30049-11623-12338-12850-8,swdocmgr_tessellation-11785-02051-00064-17409-06675-36864-49921-00003-59136-19714-02646-30819-26580-41089-55672-35842-43316-34050-55987-30975-02084-50849-12141-40614-37528-44680-42142-42646-25790-25696-00104-13084-11568-30049-11623-12338-12850-3,swdocmgr_xml-11785-02051-00064-17409-06675-36864-49921-00003-04428-18517-27868-30718-21932-04646-03361-60419-00225-10367-61457-64625-57997-52757-12143-40614-37528-44680-42142-42646-25790-25696-00104-13084-11568-30049-11623-12338-12850-836061736";

    public static string Load()
    {
        return LoadCandidates().FirstOrDefault()
               ?? throw new InvalidOperationException("未配置 SolidWorks Document Manager 内置许可证，请在 SwDocumentManagerLicense.cs 中填写 EmbeddedLicense。");
    }

    public static IReadOnlyList<string> LoadCandidates()
    {
        var candidates = new List<string>();
        AddLicenseCandidates(candidates, EmbeddedLicense);
        return candidates;
    }

    private static void AddLicenseCandidates(ICollection<string> candidates, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim().Equals("PASTE_SW_DOCUMENT_MANAGER_LICENSE_HERE", StringComparison.Ordinal))
        {
            return;
        }

        AddCandidate(candidates, value);
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(new[] { '-' }, 2, StringSplitOptions.None);
            AddCandidate(candidates, parts.Length == 2 ? parts[1] : parts[0]);
        }
    }

    private static void AddCandidate(ICollection<string> candidates, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var candidate = value.Trim();
        if (!candidates.Any(existing => string.Equals(existing, candidate, StringComparison.Ordinal)))
        {
            candidates.Add(candidate);
        }
    }
}
