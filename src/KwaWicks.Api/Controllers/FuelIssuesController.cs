using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KwaWicks.Api.Controllers;

public record ReadFuelImageRequest(string ImageBase64, string MediaType, string Field);

[ApiController]
[Route("api/fuel")]
[Produces("application/json")]
public class FuelIssuesController : ControllerBase
{
    private readonly FuelService _service;
    private readonly IHttpClientFactory _httpClientFactory;

    public FuelIssuesController(FuelService service, IHttpClientFactory httpClientFactory)
    {
        _service = service;
        _httpClientFactory = httpClientFactory;
    }

    private string CallerName =>
        User.Identity?.Name ?? User.FindFirst("cognito:username")?.Value ?? "unknown";

    // GET /api/fuel
    [HttpGet]
    [Authorize(Policy = "OperationalAccess")]
    [ProducesResponseType(typeof(List<FuelIssueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _service.ListAsync(ct));

    // POST /api/fuel
    [HttpPost]
    [Authorize(Policy = "OperationalAccess")]
    [ProducesResponseType(typeof(FuelIssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFuelIssueRequest req, CancellationToken ct)
    {
        try { return Ok(await _service.CreateAsync(req, CallerName, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/fuel/{id}/slip-upload-url?contentType=image/jpeg
    [HttpGet("{id}/slip-upload-url")]
    [Authorize(Policy = "OperationalAccess")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlipUploadUrl(
        string id, [FromQuery] string contentType, CancellationToken ct)
    {
        var ct2 = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
        var (url, key) = await _service.GetSlipUploadUrlAsync(id, ct2, ct);
        return Ok(new { uploadUrl = url, s3Key = key });
    }

    // PUT /api/fuel/{id}/slip
    [HttpPut("{id}/slip")]
    [Authorize(Policy = "OperationalAccess")]
    [ProducesResponseType(typeof(FuelIssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmSlip(string id, [FromBody] ConfirmFuelSlipRequest req, CancellationToken ct)
    {
        var dto = await _service.ConfirmSlipUploadedAsync(id, req.S3Key, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // GET /api/fuel/report?vehicleId=&from=2025-01-01&to=2025-12-31
    [HttpGet("report")]
    [Authorize(Policy = "OperationalAccess")]
    [ProducesResponseType(typeof(List<VehicleFuelReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Report(
        [FromQuery] string? vehicleId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct) =>
        Ok(await _service.GetReportAsync(vehicleId, from, to, ct));

    // POST /api/fuel/read-image
    [HttpPost("read-image")]
    [Authorize(Policy = "OperationalAccess")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReadImage([FromBody] ReadFuelImageRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ImageBase64))
            return BadRequest(new { error = "No image provided." });

        var promptText = req.Field == "odometer"
            ? "This is a photo of a vehicle odometer display. Read the odometer value shown. Return ONLY the numeric reading as a plain number with no units, no commas, no spaces. For example: 125430. If you cannot clearly read the value, return exactly: UNREADABLE"
            : "This is a photo of a fuel pump display showing the number of litres dispensed. Read the litres value shown. Return ONLY the numeric value as a plain number with up to 2 decimal places. For example: 45.20. If you cannot clearly read the value, return exactly: UNREADABLE";

        var mediaType = string.IsNullOrWhiteSpace(req.MediaType) ? "image/jpeg" : req.MediaType;

        var requestBody = new JsonObject
        {
            ["model"] = "claude-haiku-4-5-20251001",
            ["max_tokens"] = 64,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "image",
                            ["source"] = new JsonObject
                            {
                                ["type"] = "base64",
                                ["media_type"] = mediaType,
                                ["data"] = req.ImageBase64
                            }
                        },
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = promptText
                        }
                    }
                }
            }
        };

        try
        {
            var http = _httpClientFactory.CreateClient("anthropic");
            var response = await http.PostAsync(
                "https://api.anthropic.com/v1/messages",
                new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"),
                ct);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
            var text = body?["content"]?[0]?["text"]?.GetValue<string>()?.Trim() ?? "";

            if (text == "UNREADABLE" || string.IsNullOrWhiteSpace(text))
                return Ok(new { value = (double?)null, message = "Could not read the image clearly — please enter the value manually." });

            if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return Ok(new { value = parsed, message = (string?)null });

            return Ok(new { value = (double?)null, message = "Could not read the image clearly — please enter the value manually." });
        }
        catch
        {
            return Ok(new { value = (double?)null, message = "Could not read the image — please enter the value manually." });
        }
    }
}
