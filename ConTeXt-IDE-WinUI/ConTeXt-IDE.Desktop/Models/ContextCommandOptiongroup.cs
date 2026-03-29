using System.Collections.Generic;

namespace ConTeXt_IDE.Models
{
 public enum ContextCommandOptiongroupNecessity { Required, Optional }

 public enum ContextCommandOptiongroupType { Single, Names, KeyValue }

 public class ContextCommandOptiongroup
 {
  public string? Group { get; set; }

  public ContextCommandOptiongroupNecessity Necessity { get; set; }

  public ContextCommandOptiongroupType Type { get; set; }

  public List<ContextCommandOption>? Options { get; set; }
 }
}
