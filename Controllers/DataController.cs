using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Globalization;

namespace PCAOBDataExtraction.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly ILogger<DataController> _logger;
        private readonly HttpClient _httpClient;

        public DataController(ILogger<DataController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file uploaded" });
                }

                // Validate file type
                var allowedTypes = new[] { "text/csv", "application/json", "text/xml", "application/xml" };
                if (!allowedTypes.Contains(file.ContentType))
                {
                    return BadRequest(new { error = "Invalid file type. Only CSV, JSON, and XML files are allowed." });
                }

                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

                object parsedData;

                // Parse based on file type
                if (file.ContentType == "text/csv")
                {
                    parsedData = ParseCsv(content);
                }
                else if (file.ContentType == "application/json")
                {
                    parsedData = ParseJson(content);
                }
                else if (file.ContentType == "text/xml" || file.ContentType == "application/xml")
                {
                    parsedData = ParseXml(content);
                }
                else
                {
                    return BadRequest(new { error = "Unsupported file type" });
                }

                _logger.LogInformation($"Successfully processed {file.FileName}");

                return Ok(new
                {
                    success = true,
                    data = parsedData,
                    message = "File processed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing uploaded file");
                return StatusCode(500, new { error = "Error processing file: " + ex.Message });
            }
        }

        [HttpPost("fetch-url")]
        public async Task<IActionResult> FetchUrl([FromBody] UrlRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Url))
                {
                    return BadRequest(new { error = "URL is required" });
                }

                // Validate URL
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
                {
                    return BadRequest(new { error = "Invalid URL format" });
                }

                _logger.LogInformation($"Fetching data from URL: {request.Url}");

                var response = await _httpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                object parsedData;

                // Determine file type based on URL or content
                var urlLower = request.Url.ToLower();
                if (urlLower.Contains(".csv"))
                {
                    parsedData = ParseCsv(content);
                }
                else if (urlLower.Contains(".json"))
                {
                    parsedData = ParseJson(content);
                }
                else if (urlLower.Contains(".xml"))
                {
                    parsedData = ParseXml(content);
                }
                else
                {
                    // Try to auto-detect
                    try
                    {
                        parsedData = ParseJson(content);
                    }
                    catch
                    {
                        try
                        {
                            parsedData = ParseXml(content);
                        }
                        catch
                        {
                            parsedData = ParseCsv(content);
                        }
                    }
                }

                _logger.LogInformation($"Successfully processed data from URL: {request.Url}");

                return Ok(new
                {
                    success = true,
                    data = parsedData,
                    message = "URL data fetched and processed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching URL: {request.Url}");
                return StatusCode(500, new { error = "Error fetching URL: " + ex.Message });
            }
        }

        private object ParseCsv(string content)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return new List<object>();

            var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
            var result = new List<Dictionary<string, object>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                if (values.Length == headers.Length)
                {
                    var row = new Dictionary<string, object>();
                    for (int j = 0; j < headers.Length; j++)
                    {
                        row[headers[j]] = values[j];
                    }
                    result.Add(row);
                }
            }

            return result;
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var inQuotes = false;
            var currentValue = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }

            result.Add(currentValue.ToString().Trim());
            return result.ToArray();
        }

        private object ParseJson(string content)
        {
            return JsonSerializer.Deserialize<object>(content);
        }

        private object ParseXml(string content)
        {
            var doc = XDocument.Parse(content);
            return XmlToObject(doc.Root);
        }

        private object XmlToObject(XElement element)
        {
            if (element == null) return null;

            var result = new Dictionary<string, object>();

            // Add attributes
            foreach (var attribute in element.Attributes())
            {
                result[$"@{attribute.Name}"] = attribute.Value;
            }

            // Group child elements by name
            var childGroups = element.Elements().GroupBy(e => e.Name.LocalName);

            foreach (var group in childGroups)
            {
                var children = group.ToList();
                if (children.Count == 1)
                {
                    var child = children[0];
                    if (child.HasElements || child.Attributes().Any())
                    {
                        result[child.Name.LocalName] = XmlToObject(child);
                    }
                    else
                    {
                        result[child.Name.LocalName] = child.Value;
                    }
                }
                else
                {
                    result[group.Key] = children.Select(XmlToObject).ToList();
                }
            }

            // If no children and no attributes, return the text content
            if (result.Count == 0)
            {
                return element.Value;
            }

            return result;
        }
    }

    public class UrlRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}