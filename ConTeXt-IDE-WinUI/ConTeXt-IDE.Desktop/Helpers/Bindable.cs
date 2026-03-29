using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;

namespace ConTeXt_IDE.Helpers
{
 // Helper class to get the INotifyPropertyChanged models as clean an simple as possible
 public class Bindable : INotifyPropertyChanged
 {
  private Dictionary<string, object> _properties = new Dictionary<string, object>();

  public event PropertyChangedEventHandler PropertyChanged;

  protected T Get<T>(T defaultVal = default, [CallerMemberName] string name = null)
  {
   if (!_properties.TryGetValue(name, out object value))
   {
    value = _properties[name] = defaultVal;
   }
   return (T)value;
  }

  protected void Set<T>(T value, [CallerMemberName] string name = null)
  {
   //if (name != "Blocks")
    if (Equals(value, Get<T>(value, name)))
     return;
   _properties[name] = value;

   //if (name != "FileContent")
    OnPropertyChanged(name);
  }

  protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
  {
   var handlers = PropertyChanged;
   if (handlers == null)
    return;

   void RaiseHandlers()
   {
    var args = new PropertyChangedEventArgs(propertyName);
    foreach (PropertyChangedEventHandler handler in handlers.GetInvocationList().OfType<PropertyChangedEventHandler>())
    {
     try
     {
      handler(this, args);
     }
     catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException || ex is InvalidOperationException)
     {
     }
    }
   }

   try
   {
    var queue = DispatcherQueue.GetForCurrentThread();
    if (queue != null && !queue.HasThreadAccess)
    {
     if (!queue.TryEnqueue(RaiseHandlers))
      RaiseHandlers();
    }
    else
    {
     RaiseHandlers();
    }
   }
   catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException || ex is InvalidOperationException)
   {
   }
  }
 }
}
