using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Models;

public class ChildSaveModel : ChildUpdateModel
{
    [Range(1, 10)]
    public int ClassLevel { get; set; }
}
