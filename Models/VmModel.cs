using System.ComponentModel.DataAnnotations;

namespace CloudAuthApp.Models;

public class VmModel
{
    [Key] public int Id { get; set; }

    public string Name { get; set; }

    public string IpPublic { get; set; }

    [Required] public string Login { get; set; }

    [Required] public string Password { get; set; }
    
    public bool IsRunning { get; set; }
}