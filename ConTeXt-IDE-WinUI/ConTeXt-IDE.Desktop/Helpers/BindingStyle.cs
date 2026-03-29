using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ConTeXt_IDE.Helpers;

[ContentProperty(Name = nameof(Setters))]
public class BindingStyle
{
 /// <summary>
 /// The collection of <see cref="BindingSetter"/>'s.
 /// Normally this value is not assigned directly, but
 /// is populated through XAML.
 /// </summary>
 public Collection<BindingSetter> Setters
 {
  get => _setters ?? (_setters = new Collection<BindingSetter>());
  set => _setters = value;
 }

 #region SmartStyle Style attached property
 /// <summary>
 /// An attached DependencyProperty for getting or setting
 /// a <see cref="BindingStyle"/> on a FrameworkElement.
 /// </summary>
 public static readonly DependencyProperty StyleProperty = DependencyProperty.RegisterAttached(
				 "Style",
				 typeof(BindingStyle),
				 typeof(BindingStyle),
				 new PropertyMetadata(
								 (BindingStyle)null,
								 (obj, args) =>
								 {
								  if (!(obj is FrameworkElement fe) || !(args.NewValue is BindingStyle style))
									return;
								  foreach (var s in style.Setters)
								  {
									if (string.IsNullOrEmpty(s.PropertyName))
									 throw new ArgumentNullException(nameof(s.PropertyName));
									if (s.Binding == null)
									 throw new ArgumentNullException(nameof(s.Binding));
									var dp = s.ResolveProperty(fe.GetType());
									if (dp == null)
									 throw new InvalidOperationException(
																					$"Could not locate {s.PropertyName}Property on {fe.GetType()}; " +
																					$"did you forget to specify {nameof(s.PropertyOwner)}?");
									BindingOperations.SetBinding(obj, dp, s.Binding);
								  }
								 }));
 public static BindingStyle GetStyle(DependencyObject obj)
 {
  return (BindingStyle)obj.GetValue(StyleProperty);
 }
 public static void SetStyle(DependencyObject obj, BindingStyle style)
 {
  obj.SetValue(StyleProperty, style);
 }
 #endregion

 private Collection<BindingSetter> _setters;
}

public class BindingSetter
{
 /// <summary>
 /// The target <see cref="DependencyProperty"/> name, e.g.,
 /// <c>Background</c>. For an attached property, ensure the 
 /// <see cref="PropertyOwner"/> property is set, since this
 /// class doesn't have access to XAML namespaces. While this
 /// uses reflection the amount is negligible as the target property
 /// is cached after resolution.
 /// </summary>
 public string PropertyName
 {
  get;
  set;
 }

 /// <summary>
 /// References the DependencyProperty owner when attached
 /// properties are targeted. Otherwise the owner is assumed
 /// to be the type of the FrameworkElement being styled.
 /// </summary>
 public Type PropertyOwner
 {
  get;
  set;
 }

 /// <summary>
 /// A BindingBase. In XAML, use normal binding notation, e.g.,
 /// <c>"{Binding RelativeSource={RelativeSource Mode=Self}, Path=SomeOtherProperty}"</c>
 /// </summary>
 public BindingBase Binding
 {
  get;
  set;
 }

 internal DependencyProperty ResolveProperty(Type ownerType)
 {
  if (_resolvedProperty != null)
	return _resolvedProperty;
  return _resolvedProperty = (PropertyOwner ?? ownerType).TryGetDependencyProperty(this.PropertyName);
 }

 DependencyProperty _resolvedProperty;
}
internal static class Extensions
{
 public static DependencyProperty TryGetDependencyProperty(this Type type, string propertyName)
 {
  // In WinUI the DP instances of native objects are
  // exposed as properties rather than fields, I guess
  // because of COM and stuff.
  var dpProp = type.GetProperty(
				  $"{propertyName}Property",
				  BindingFlags.Static
								  | BindingFlags.FlattenHierarchy
								  | BindingFlags.Public);
  if (dpProp != null)
	return dpProp.GetValue(null) as DependencyProperty;

  var dpField = type.GetField(
				  $"{propertyName}Property",
				  BindingFlags.Static
								  | BindingFlags.FlattenHierarchy
								  | BindingFlags.Public);
  if (dpField != null)
	return dpField.GetValue(null) as DependencyProperty;

  return null;
 }
}