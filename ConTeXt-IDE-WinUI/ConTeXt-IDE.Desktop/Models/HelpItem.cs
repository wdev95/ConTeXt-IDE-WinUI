using ConTeXt_IDE.Helpers;

namespace ConTeXt_IDE.Models
{
 public class HelpItem : Bindable
 {
  public string ID { get => Get(""); set => Set(value); }

  public string Title { get => Get("Help"); set => Set(value); }

  public string Subtitle { get => Get(""); set => Set(value); }

  public string Text { get => Get(""); set => Set(value); }

  public bool Shown { get => Get(false); set => Set(value); }
 }
}
