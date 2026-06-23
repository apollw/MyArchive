using System.Text;
using Microsoft.AspNetCore.Mvc;
using MyArchive.Services;
using MyArchive.Shared;

namespace MyArchive.Controllers;

[ApiController]
[Route("api/library")]
public sealed class LibraryController(ArchiveService archiveService) : ControllerBase
{
    [HttpGet("export/json")]
    public async Task<IActionResult> ExportJson()
        => File(
            Encoding.UTF8.GetBytes(await archiveService.ExportJsonAsync()),
            "application/json; charset=utf-8",
            $"myarchive-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv()
        => File(
            Encoding.UTF8.GetBytes(await archiveService.ExportCsvAsync()),
            "text/csv; charset=utf-8",
            $"myarchive-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");

    [HttpGet("export/markdown")]
    public async Task<IActionResult> ExportMarkdown()
        => File(
            Encoding.UTF8.GetBytes(await archiveService.ExportMarkdownAsync()),
            "text/markdown; charset=utf-8",
            $"myarchive-{DateTime.UtcNow:yyyyMMdd-HHmm}.md");

    [HttpPost("import")]
    public async Task<ActionResult<ImportSummary>> Import([FromQuery] ImportFormat format, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Arquivo de importacao nao enviado.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await archiveService.ImportAsync(stream, format));
    }
}
