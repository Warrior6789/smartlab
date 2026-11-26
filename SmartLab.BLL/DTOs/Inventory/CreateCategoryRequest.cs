using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartLab.BLL.DTOs.Inventory;

public class CreateCategoryRequest
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
