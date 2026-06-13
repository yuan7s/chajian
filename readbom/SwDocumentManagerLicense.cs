namespace readbom;

internal static class SwDocumentManagerLicense
{
    private const string EmbeddedLicense = "SOLIDWORKS_2022:swdocmgr_general-11785-02051-00064-50177-06612-36864-49921-00003-21424-27712-02333-41243-17858-56070-14468-62467-35817-38164-41304-28396-16118-35795-11666-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-4,swdocmgr_previews-11785-02051-00064-50177-06612-36864-49921-00003-55360-53060-25343-42769-59069-52473-22473-52225-12074-04081-32909-30728-28589-33225-11507-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-3,swdocmgr_geometry-11785-02051-00064-50177-06612-36864-49921-00003-47348-23774-43673-03705-12160-02780-32459-34818-63205-17331-46285-63135-49863-07899-11787-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-2,swdocmgr_dimxpert-11785-02051-00064-50177-06612-36864-49921-00003-29996-08472-47529-56591-62456-46237-10843-31747-32567-27053-59927-17093-07167-05996-11461-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-7,swdocmgr_tessellation-11785-02051-00064-50177-06612-36864-49921-00003-10436-56910-32567-34976-20928-11850-62944-24577-40211-48738-24555-37037-32570-31276-11315-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-9,swdocmgr_xml-11785-02051-00064-50177-06612-36864-49921-00003-42016-05489-35279-24819-52264-14236-16400-48131-43581-35953-11290-28017-22653-22728-11882-40614-37528-44680-42142-42646-25790-25696-00100-12572-11577-30049-11623-12338-12338-8";

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
