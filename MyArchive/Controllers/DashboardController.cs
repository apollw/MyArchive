using Microsoft.AspNetCore.Mvc;
using MyArchive.Services;
using MyArchive.Shared;

namespace MyArchive.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(ArchiveService archiveService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardSummary>> GetDashboard()
        => Ok(await archiveService.GetDashboardAsync());
}
