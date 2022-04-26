using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

using JetBrains.Core;
using JetBrains.Diagnostics;
using JetBrains.Collections;
using JetBrains.Collections.Viewable;
using JetBrains.Lifetimes;
using JetBrains.Serialization;
using JetBrains.Rd;
using JetBrains.Rd.Base;
using JetBrains.Rd.Impl;
using JetBrains.Rd.Tasks;
using JetBrains.Rd.Util;
using JetBrains.Rd.Text;


// ReSharper disable RedundantEmptyObjectCreationArgumentList
// ReSharper disable InconsistentNaming
// ReSharper disable RedundantOverflowCheckingContext


namespace JetBrains.Rider.Model
{
  
  
  public class LiveSharpModel : RdExtBase
  {
    //fields
    //public fields
    [NotNull] public IViewableProperty<JetBrains.Rider.Model.MyEnum?> MyEnum => _MyEnum;
    [NotNull] public ISource<JetBrains.Rider.Model.FileEvent> FileEvent => _FileEvent;
    
    //private fields
    [NotNull] private readonly RdProperty<JetBrains.Rider.Model.MyEnum?> _MyEnum;
    [NotNull] private readonly RdSignal<JetBrains.Rider.Model.FileEvent> _FileEvent;
    
    //primary constructor
    private LiveSharpModel(
      [NotNull] RdProperty<JetBrains.Rider.Model.MyEnum?> myEnum,
      [NotNull] RdSignal<JetBrains.Rider.Model.FileEvent> fileEvent
    )
    {
      if (myEnum == null) throw new ArgumentNullException("myEnum");
      if (fileEvent == null) throw new ArgumentNullException("fileEvent");
      
      _MyEnum = myEnum;
      _FileEvent = fileEvent;
      _MyEnum.OptimizeNested = true;
      _MyEnum.ValueCanBeNull = true;
      BindableChildren.Add(new KeyValuePair<string, object>("myEnum", _MyEnum));
      BindableChildren.Add(new KeyValuePair<string, object>("fileEvent", _FileEvent));
    }
    //secondary constructor
    internal LiveSharpModel (
    ) : this (
      new RdProperty<JetBrains.Rider.Model.MyEnum?>(ReadMyEnumNullable, WriteMyEnumNullable),
      new RdSignal<JetBrains.Rider.Model.FileEvent>(JetBrains.Rider.Model.FileEvent.Read, JetBrains.Rider.Model.FileEvent.Write)
    ) {}
    //statics
    
    public static CtxReadDelegate<JetBrains.Rider.Model.MyEnum?> ReadMyEnumNullable = new CtxReadDelegate<JetBrains.Rider.Model.MyEnum>(JetBrains.Rd.Impl.Serializers.ReadEnum<JetBrains.Rider.Model.MyEnum>).NullableStruct();
    
    public static CtxWriteDelegate<JetBrains.Rider.Model.MyEnum?> WriteMyEnumNullable = new CtxWriteDelegate<JetBrains.Rider.Model.MyEnum>(JetBrains.Rd.Impl.Serializers.WriteEnum<JetBrains.Rider.Model.MyEnum>).NullableStruct();
    
    protected override long SerializationHash => -6704738345056866L;
    
    protected override Action<ISerializers> Register => RegisterDeclaredTypesSerializers;
    public static void RegisterDeclaredTypesSerializers(ISerializers serializers)
    {
      
      serializers.RegisterToplevelOnce(typeof(IdeRoot), IdeRoot.RegisterDeclaredTypesSerializers);
    }
    
    //custom body
    //equals trait
    //hash code trait
    //pretty print
    public override void Print(PrettyPrinter printer)
    {
      printer.Println("LiveSharpModel (");
      using (printer.IndentCookie()) {
        printer.Print("myEnum = "); _MyEnum.PrintEx(printer); printer.Println();
        printer.Print("fileEvent = "); _FileEvent.PrintEx(printer); printer.Println();
      }
      printer.Print(")");
    }
    //toString
    public override string ToString()
    {
      var printer = new SingleLinePrettyPrinter();
      Print(printer);
      return printer.ToString();
    }
  }
  public static class SolutionLiveSharpModelEx
   {
    public static LiveSharpModel GetLiveSharpModel(this Solution solution)
    {
      return solution.GetOrCreateExtension("liveSharpModel", () => new LiveSharpModel());
    }
  }
  
  
  public class FileEvent : IPrintable, IEquatable<FileEvent>
  {
    //fields
    //public fields
    [NotNull] public string FileName {get; private set;}
    [NotNull] public string Data {get; private set;}
    [NotNull] public char[] Data_array {get; private set;}
    [NotNull] public string EventType {get; private set;}
    
    //private fields
    //primary constructor
    public FileEvent(
      [NotNull] string fileName,
      [NotNull] string data,
      [NotNull] char[] data_array,
      [NotNull] string eventType
    )
    {
      if (fileName == null) throw new ArgumentNullException("fileName");
      if (data == null) throw new ArgumentNullException("data");
      if (data_array == null) throw new ArgumentNullException("data_array");
      if (eventType == null) throw new ArgumentNullException("eventType");
      
      FileName = fileName;
      Data = data;
      Data_array = data_array;
      EventType = eventType;
    }
    //secondary constructor
    //statics
    
    public static CtxReadDelegate<FileEvent> Read = (ctx, reader) => 
    {
      var fileName = reader.ReadString();
      var data = reader.ReadString();
      var data_array = JetBrains.Rd.Impl.Serializers.ReadCharArray(ctx, reader);
      var eventType = reader.ReadString();
      var _result = new FileEvent(fileName, data, data_array, eventType);
      return _result;
    };
    
    public static CtxWriteDelegate<FileEvent> Write = (ctx, writer, value) => 
    {
      writer.Write(value.FileName);
      writer.Write(value.Data);
      JetBrains.Rd.Impl.Serializers.WriteCharArray(ctx, writer, value.Data_array);
      writer.Write(value.EventType);
    };
    //custom body
    //equals trait
    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != GetType()) return false;
      return Equals((FileEvent) obj);
    }
    public bool Equals(FileEvent other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return FileName == other.FileName && Data == other.Data && Data_array.SequenceEqual(other.Data_array) && EventType == other.EventType;
    }
    //hash code trait
    public override int GetHashCode()
    {
      unchecked {
        var hash = 0;
        hash = hash * 31 + FileName.GetHashCode();
        hash = hash * 31 + Data.GetHashCode();
        hash = hash * 31 + Data_array.ContentHashCode();
        hash = hash * 31 + EventType.GetHashCode();
        return hash;
      }
    }
    //pretty print
    public void Print(PrettyPrinter printer)
    {
      printer.Println("FileEvent (");
      using (printer.IndentCookie()) {
        printer.Print("fileName = "); FileName.PrintEx(printer); printer.Println();
        printer.Print("data = "); Data.PrintEx(printer); printer.Println();
        printer.Print("data_array = "); Data_array.PrintEx(printer); printer.Println();
        printer.Print("eventType = "); EventType.PrintEx(printer); printer.Println();
      }
      printer.Print(")");
    }
    //toString
    public override string ToString()
    {
      var printer = new SingleLinePrettyPrinter();
      Print(printer);
      return printer.ToString();
    }
  }
  
  
  public enum MyEnum {
    FirstValue,
    SecondVaddlue
  }
}
