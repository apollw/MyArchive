using Microsoft.AspNetCore.Mvc;
using MyArchive.Services;
using MyArchive.Shared;

namespace MyArchive.Controllers;

[ApiController]
[Route("api/items")]
public sealed class ItemsController(ArchiveService archiveService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ArchiveCatalog>> GetCatalog([FromQuery] ArchiveQuery query)
        => Ok(await archiveService.GetCatalogAsync(query));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemEditorModel>> GetItem(Guid id)
    {
        var item = await archiveService.GetItemAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ItemEditorModel>> CreateItem([FromBody] ItemEditorModel model)
    {
        var id = await archiveService.SaveItemAsync(model);
        var created = await archiveService.GetItemAsync(id);
        return CreatedAtAction(nameof(GetItem), new { id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ItemEditorModel>> UpdateItem(Guid id, [FromBody] ItemEditorModel model)
    {
        model.Id = id;
        var savedId = await archiveService.SaveItemAsync(model);
        var updated = await archiveService.GetItemAsync(savedId);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        await archiveService.DeleteItemAsync(id);
        return NoContent();
    }
}
