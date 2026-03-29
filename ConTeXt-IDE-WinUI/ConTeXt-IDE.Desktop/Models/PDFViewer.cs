using ConTeXt_IDE.Helpers;

namespace ConTeXt_IDE.Models
{
 public class PDFViewer : Bindable
 {
  public PDFViewer(string name = "", string path = "")
  {
	Name = name;
	Path = path;
  }
  public string Name { get => Get(""); set => Set(value); }
  public string Path { get => Get(""); set => Set(value); }
 }
}
