using System.Collections;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using SolidWorks.Interop.swdocumentmgr;

namespace readbom;

internal static partial class OfflineSolidWorksReader
{
    private static BomRow CreateBomRow(
        ISwDMDocument document,
        string path,
        string configuration,
        int quantity,
        IReadOnlyList<PropertyMappingItem> propertiesToRead)
    {
        var documentProperties = ReadDocumentProperties(document);
        var configurationProperties = ReadConfigurationProperties(ResolveConfiguration(document, configuration));
        var documentType = GetDocumentTypeLabel(path);
        var material = GetDocumentType(path) == SwDmDocumentType.swDmDocumentPart
            ? ReadMaterial(document, configuration)
            : "无需设置";
        if (documentType == "零件" && string.IsNullOrWhiteSpace(material))
        {
            material = "未设置";
        }

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        properties[PropertyMappingConfig.FolderNamePropertyName] = Path.GetDirectoryName(path) ?? string.Empty;
        properties[PropertyMappingConfig.FileNamePropertyName] = Path.GetFileNameWithoutExtension(path);
        properties[PropertyMappingConfig.QuantityPropertyName] = quantity.ToString(CultureInfo.InvariantCulture);
        foreach (var item in propertiesToRead.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            var sourceProperties = item.SourceMode == PropertySourceMode.CurrentConfiguration
                ? configurationProperties
                : documentProperties;
            properties[item.Name] = GetPropertyValue(sourceProperties, item.Name);
        }

        var hasDrawing = DrawingFileHelper.HasSiblingDrawing(path);
        return new BomRow
        {
            DocumentType = documentType,
            DocumentIconPath = GetDocumentIconPath(path),
            DrawingStatus = hasDrawing ? "有工程图" : "无工程图",
            DrawingIconPath = hasDrawing ? "pack://application:,,,/Assets/drawing.png" : string.Empty,
            FileName = Path.GetFileNameWithoutExtension(path),
            Configuration = string.IsNullOrWhiteSpace(configuration) ? "Default" : configuration,
            Quantity = quantity,
            Material = material,
            Properties = properties,
            OriginalProperties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase),
            AvailablePropertyNames = documentProperties.Keys
                .Concat(configurationProperties.Keys)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            FullPath = path
        };
    }

    private static Dictionary<string, string> ReadDocumentProperties(ISwDMDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetPropertyNames(document.GetCustomPropertyNames))
        {
            try
            {
                var type = SwDmCustomInfoType.swDmCustomInfoUnknown;
                var linkedTo = string.Empty;
                var value = document is ISwDMDocument5 document5
                    ? document5.GetCustomPropertyValues(name, out type, out linkedTo)
                    : document.GetCustomProperty(name, out type);
                AddProperty(result, name, value);
            }
            catch
            {
            }
        }

        return result;
    }

    private static Dictionary<string, string> ReadConfigurationProperties(ISwDMConfiguration? configuration)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (configuration is null)
        {
            return result;
        }

        foreach (var name in GetPropertyNames(configuration.GetCustomPropertyNames))
        {
            try
            {
                var type = SwDmCustomInfoType.swDmCustomInfoUnknown;
                var linkedTo = string.Empty;
                var value = configuration is ISwDMConfiguration5 configuration5
                    ? configuration5.GetCustomPropertyValues(name, out type, out linkedTo)
                    : configuration.GetCustomProperty(name, out type);
                AddProperty(result, name, value);
            }
            catch
            {
            }
        }

        return result;
    }

    private static IEnumerable<string> GetPropertyNames(Func<object> getNames)
    {
        try
        {
            return ToStringList(getNames())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static void AddProperty(IDictionary<string, string> properties, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        properties[name.Trim()] = NormalizeValue(value);
    }

    private static string GetPropertyValue(IReadOnlyDictionary<string, string> properties, string propertyName)
    {
        return properties.TryGetValue(propertyName, out var value) ? value : string.Empty;
    }

    private static string ReadMaterial(ISwDMDocument document, string configuration)
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"readbom-keywords-{Guid.NewGuid():N}.xml");
        try
        {
            if (document is not ISwDMDocument28 document28)
            {
                return string.Empty;
            }

            var error = document28.GetKeyWordStream(xmlPath);
            if (error != SwDmXmlDataError.swDmXmlDataErrorNone || !File.Exists(xmlPath))
            {
                return string.Empty;
            }

            return ReadMaterialFromKeywordXml(xmlPath, configuration);
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            TryDeleteFile(xmlPath);
        }
    }

    private static string ReadMaterialFromKeywordXml(string xmlPath, string configuration)
    {
        try
        {
            var document = XDocument.Load(xmlPath);
            return FindMaterialValues(document.Root, configuration)
                .OrderByDescending(x => x.Score)
                .Select(x => NormalizeValue(x.Value))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IEnumerable<MaterialCandidate> FindMaterialValues(XElement? root, string configuration)
    {
        if (root is null)
        {
            yield break;
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            if (IsPropertyTableElement(element))
            {
                continue;
            }

            foreach (var attribute in element.Attributes())
            {
                if (IsMaterialFieldName(attribute.Name.LocalName) && IsValidMaterialValue(attribute.Value))
                {
                    yield return new MaterialCandidate(attribute.Value, ScoreMaterialCandidate(element, configuration, 2));
                }
            }

            if (IsMaterialFieldName(element.Name.LocalName) && !element.Elements().Any() && IsValidMaterialValue(element.Value))
            {
                yield return new MaterialCandidate(element.Value, ScoreMaterialCandidate(element, configuration, 3));
            }
        }
    }

    private static int ScoreMaterialCandidate(XElement element, string configuration, int baseScore)
    {
        var score = baseScore;
        if (!string.IsNullOrWhiteSpace(configuration)
            && element.AncestorsAndSelf().Any(x => ElementMentionsConfiguration(x, configuration)))
        {
            score += 10;
        }

        return score;
    }

    private static bool ElementMentionsConfiguration(XElement element, string configuration)
    {
        return (element.Name.LocalName.Contains("config", StringComparison.OrdinalIgnoreCase)
                && element.Value.Contains(configuration, StringComparison.OrdinalIgnoreCase))
               || element.Attributes().Any(attribute =>
                   attribute.Value.Contains(configuration, StringComparison.OrdinalIgnoreCase)
                   || (attribute.Name.LocalName.Contains("config", StringComparison.OrdinalIgnoreCase)
                       && attribute.Value.Contains(configuration, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsPropertyTableElement(XElement element)
    {
        return element.AncestorsAndSelf().Any(x =>
        {
            var name = x.Name.LocalName;
            return name.Contains("custom", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("property", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("properties", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("attribute", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("summary", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("配置属性", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("自定义属性", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsMaterialFieldName(string value)
    {
        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty);
        return normalized.Equals("material", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("materialname", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("materialidname", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("materialpropertyname", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("materialusername", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("swmaterial", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("材料", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("材质", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidMaterialValue(string value)
    {
        var normalized = NormalizeValue(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return !normalized.Contains("$PRP", StringComparison.OrdinalIgnoreCase)
               && !normalized.Contains("SW-Material@", StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("material", StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("materials", StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("材料", StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("材质", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed record MaterialCandidate(string Value, int Score);

    private static ISwDMConfiguration? ResolveConfiguration(ISwDMDocument document, string? configuration)
    {
        foreach (var candidate in GetConfigurationCandidates(document, configuration))
        {
            try
            {
                var resolved = document.ConfigurationManager.GetConfigurationByName(candidate) as ISwDMConfiguration;
                if (resolved is not null)
                {
                    return resolved;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> GetConfigurationCandidates(ISwDMDocument document, string? configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration) && !configuration.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            yield return configuration.Trim();
        }

        var active = GetActiveConfigurationName(document);
        if (!string.IsNullOrWhiteSpace(active))
        {
            yield return active;
        }

        foreach (var name in GetConfigurationNames(document))
        {
            yield return name;
        }
    }

    private static string NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static List<string> ToStringList(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        var result = new List<string>();
        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var textValue = item?.ToString();
                if (!string.IsNullOrWhiteSpace(textValue))
                {
                    result.Add(textValue);
                }
            }
        }

        return result;
    }
}
