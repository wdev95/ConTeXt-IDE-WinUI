
using ConTeXt_IDE.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ConTeXt_IDE
{
    public sealed partial class SelectTemplate : ContentDialog
    {
        public SelectTemplate()
        {
            this.InitializeComponent();
        }

        public ObservableCollection<TemplateSelection> templateSelections = new ObservableCollection<TemplateSelection>() {
         new TemplateSelection(){ Content = "Hello World (MWE)", Tag = "mwe", IsSelected = false},
         new TemplateSelection(){ Content = "Single File with basic layouting", Tag = "single", IsSelected = true},
          
          new TemplateSelection(){ Content = "Project structure for a thesis", Tag = "projthes", IsSelected = false},
          new TemplateSelection(){ Content = "Project structure for a stepped presentation", Tag = "projpres", IsSelected = false},

          new TemplateSelection(){ Content = "Curriculum Vitae", Tag = "cv", IsSelected = false},
          new TemplateSelection(){ Content = "Mardown compiler (filter & Pandoc)", Tag = "markdown", IsSelected = false},
        };

    }
}
