using System.Collections.Generic;

namespace ConTeXt_IDE.Models
{
 public class ContextCommand(string command, List<ContextCommandOptiongroup> optiongroups)
 {
  public string Command { get; set; } = command;

  public List<ContextCommandOptiongroup> Optiongroups { get; set; } = optiongroups;
 }
}
