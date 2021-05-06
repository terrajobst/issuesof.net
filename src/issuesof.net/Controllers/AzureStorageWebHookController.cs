using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IssuesOfDotNet.Controllers
{
    [ApiController]
    [Route("azure-storage-web-hook")]
    [AllowAnonymous]
    public class AzureStorageWebHookController : Controller
    {
        private readonly ILogger<AzureStorageWebHookController> _logger;
        private readonly IndexService _indexService;

        public AzureStorageWebHookController(ILogger<AzureStorageWebHookController> logger,
                                             IndexService indexService)
        {
            _logger = logger;
            _indexService = indexService;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var contentType = new ContentType(Request.ContentType);

            if (contentType.MediaType != MediaTypeNames.Application.Json)
            {
                _logger.LogError("Received invalid content type {0}", contentType.MediaType);
                return BadRequest();
            }

            string json;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                json = await reader.ReadToEndAsync();

            if (json.Contains("index.cicache"))
            {
                _logger.LogInformation("Received request to update index");
                _indexService.Reload();
                return Ok();
            }

            _logger.LogInformation("Received non-index related request with payload {0}", json);

            // OK, check for event subscription request

            try
            {
                var payloads = JsonSerializer.Deserialize<Payload[]>(json);
                if (payloads is not null &&
                    payloads.Length > 0 &&
                    payloads[0].data is not null &&
                    !string.IsNullOrEmpty(payloads[0].data.validationCode))
                {
                    var validationCode = payloads[0].data.validationCode;
                    _logger.LogInformation("Received subscription confirmation request with code {0}", validationCode);
                    return Ok($"{{ \"validationResponse\": \"{validationCode}\" }}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Couldn't parse payload: {json}");
            }

            return BadRequest();
        }

        public class Payload
        {
            public string id { get; set; }
            public string topic { get; set; }
            public string subject { get; set; }
            public PayloadData data { get; set; }
            public string eventType { get; set; }
            public DateTime eventTime { get; set; }
            public string metadataVersion { get; set; }
            public string dataVersion { get; set; }
        }

        public class PayloadData
        {
            public string validationCode { get; set; }
            public string validationUrl { get; set; }
        }
    }
}
