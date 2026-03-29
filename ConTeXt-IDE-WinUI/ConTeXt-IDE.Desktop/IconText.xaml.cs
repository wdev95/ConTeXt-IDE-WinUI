using CodeEditorControl_WinUI;
using ConTeXt_IDE.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ConTeXt_IDE
{
 public sealed partial class IconText : UserControl
 {
  public static readonly DependencyProperty SymbolProperty =
	  DependencyProperty.RegisterAttached(
		  nameof(Symbol),
		  typeof(FontIconSymbol),
		  typeof(IconText),
		  new PropertyMetadata(default(FontIconSymbol), OnSymbolChanged));
  private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
  {
	if (d is IconText iconText && e.NewValue is FontIconSymbol newSymbol)
	{
	 iconText.FontIcon.Glyph = char.ConvertFromUtf32((int)newSymbol);
	}
  }

  public static readonly DependencyProperty TextProperty =
	  DependencyProperty.RegisterAttached(
		  nameof(Text),
		  typeof(string),
		  typeof(IconText),
		  new PropertyMetadata(string.Empty, OnTextChanged));
  private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
  {
	if (d is IconText iconText && e.NewValue is string newText)
	{
	 iconText.TextBlock.Text = newText;
	}
  }

  public FontIconSymbol Symbol
  {
	get => (FontIconSymbol)GetValue(SymbolProperty);
	set => SetValue(SymbolProperty, value);
  }
  public string Text
  {
	get => (string)GetValue(TextProperty);
	set => SetValue(TextProperty, value);
  }

  public IconText()
  {
	this.InitializeComponent();
  }
 }
}