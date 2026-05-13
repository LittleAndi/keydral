using System.Globalization;
using System.Text.RegularExpressions;

namespace Keydral.API.Search;

/// <summary>
/// Parses a lightweight AND-only query DSL into secret and audit search requests.
/// </summary>
public static partial class SearchQueryParser
{
    private static readonly Regex AndSplitter = CreateAndSplitter();

    public static (SecretSearchRequest Secrets, AuditLogSearchRequest AuditLogs) Parse(
        string query,
        int pageNumber,
        int pageSize)
    {
        var secretRequest = new SecretSearchRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        var auditRequest = new AuditLogSearchRequest
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        if (string.IsNullOrWhiteSpace(query))
        {
            return (secretRequest, auditRequest);
        }

        foreach (var rawClause in AndSplitter.Split(query))
        {
            var clause = rawClause.Trim();
            if (string.IsNullOrWhiteSpace(clause))
            {
                continue;
            }

            var separatorIndex = clause.IndexOf(':');
            if (separatorIndex <= 0)
            {
                AppendFreeText(secretRequest, auditRequest, Unquote(clause));
                continue;
            }

            var field = clause[..separatorIndex].Trim().ToLowerInvariant();
            var value = clause[(separatorIndex + 1)..].Trim();

            switch (field)
            {
                case "name":
                    secretRequest.NamePattern = Unquote(value);
                    break;
                case "description":
                    AppendSecretQuery(secretRequest, Unquote(value));
                    break;
                case "tags":
                    var tag = Unquote(value);
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        secretRequest.Tags.Add(tag);
                    }

                    break;
                case "created":
                    ApplyRange(value, assignStart: date => secretRequest.CreatedAfter = date, assignEnd: date => secretRequest.CreatedBefore = date);
                    break;
                case "updated":
                    ApplyRange(value, assignStart: date => secretRequest.UpdatedAfter = date, assignEnd: date => secretRequest.UpdatedBefore = date);
                    break;
                case "created-by":
                case "createdby":
                    secretRequest.CreatedBy = Unquote(value);
                    break;
                case "actor":
                    auditRequest.Actor = Unquote(value);
                    break;
                case "action":
                    auditRequest.Action = Unquote(value);
                    break;
                case "result":
                    auditRequest.Result = Unquote(value);
                    break;
                case "resource-type":
                case "resourcetype":
                    auditRequest.ResourceType = Unquote(value);
                    break;
                case "resource-id":
                case "resourceid":
                    auditRequest.ResourceId = Unquote(value);
                    break;
                case "timestamp":
                case "date":
                    ApplyRange(value, assignStart: date => auditRequest.FromDate = date, assignEnd: date => auditRequest.ToDate = date);
                    break;
                default:
                    AppendFreeText(secretRequest, auditRequest, Unquote(value));
                    break;
            }
        }

        return (secretRequest, auditRequest);
    }

    private static void AppendFreeText(SecretSearchRequest secretRequest, AuditLogSearchRequest auditRequest, string value)
    {
        AppendSecretQuery(secretRequest, value);
        AppendAuditQuery(auditRequest, value);
    }

    private static void AppendSecretQuery(SecretSearchRequest request, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Query = string.IsNullOrWhiteSpace(request.Query) ? value : $"{request.Query} {value}";
        }
    }

    private static void AppendAuditQuery(AuditLogSearchRequest request, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Query = string.IsNullOrWhiteSpace(request.Query) ? value : $"{request.Query} {value}";
        }
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static void ApplyRange(string value, Action<DateTime?> assignStart, Action<DateTime?> assignEnd)
    {
        var trimmed = value.Trim();
        if (!(trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            return;
        }

        var parts = trimmed[1..^1].Split(" TO ", StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return;
        }

        assignStart(ParseDate(parts[0]));
        assignEnd(ParseDate(parts[1]));
    }

    private static DateTime? ParseDate(string value)
    {
        if (value == "*")
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    [GeneratedRegex(@"\s+AND\s+", RegexOptions.IgnoreCase)]
    private static partial Regex CreateAndSplitter();
}
