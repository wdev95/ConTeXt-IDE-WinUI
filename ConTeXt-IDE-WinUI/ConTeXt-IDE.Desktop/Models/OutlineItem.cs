using ConTeXt_IDE.Helpers;
namespace ConTeXt_IDE.Models
{
 public class OutlineItem : Bindable
 {
  public int ID { get => Get(0); set => Set(value); }
  public int SectionLevel { get => Get(0); set => Set(value); }
  public string SectionType { get => Get<string>(); set => Set(value); }

  public string Title { get => Get<string>(); set => Set(value); }

  public int Row { get => Get(0); set => Set(value); }
 }
}
