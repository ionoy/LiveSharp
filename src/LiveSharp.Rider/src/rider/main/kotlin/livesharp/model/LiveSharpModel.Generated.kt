@file:Suppress("PackageDirectoryMismatch", "UnusedImport", "unused", "LocalVariableName")
package com.jetbrains.rider.model

import com.jetbrains.rd.framework.*
import com.jetbrains.rd.framework.base.*
import com.jetbrains.rd.framework.impl.*

import com.jetbrains.rd.util.lifetime.*
import com.jetbrains.rd.util.reactive.*
import com.jetbrains.rd.util.string.*
import com.jetbrains.rd.util.*
import kotlin.reflect.KClass



class LiveSharpModel private constructor(
    private val _myEnum: RdProperty<MyEnum?>,
    private val _fileEvent: RdSignal<FileEvent>
) : RdExtBase() {
    //companion
    
    companion object : ISerializersOwner {
        
        override fun registerSerializersCore(serializers: ISerializers) {
            serializers.register(MyEnum.marshaller)
            serializers.register(FileEvent)
        }
        
        
        
        private val __MyEnumNullableSerializer = MyEnum.marshaller.nullable()
        
        const val serializationHash = -6704738345056866L
    }
    override val serializersOwner: ISerializersOwner get() = LiveSharpModel
    override val serializationHash: Long get() = LiveSharpModel.serializationHash
    
    //fields
    val myEnum: IProperty<MyEnum?> get() = _myEnum
    val fileEvent: ISignal<FileEvent> get() = _fileEvent
    //initializer
    init {
        _myEnum.optimizeNested = true
    }
    
    init {
        bindableChildren.add("myEnum" to _myEnum)
        bindableChildren.add("fileEvent" to _fileEvent)
    }
    
    //secondary constructor
    internal constructor(
    ) : this(
        RdProperty<MyEnum?>(null, __MyEnumNullableSerializer),
        RdSignal<FileEvent>(FileEvent)
    )
    
    //equals trait
    //hash code trait
    //pretty print
    override fun print(printer: PrettyPrinter) {
        printer.println("LiveSharpModel (")
        printer.indent {
            print("myEnum = "); _myEnum.print(printer); println()
            print("fileEvent = "); _fileEvent.print(printer); println()
        }
        printer.print(")")
    }
}
val Solution.liveSharpModel get() = getOrCreateExtension("liveSharpModel", ::LiveSharpModel)



data class FileEvent (
    val fileName: String,
    val data: String,
    val data_array: CharArray,
    val eventType: String
) : IPrintable {
    //companion
    
    companion object : IMarshaller<FileEvent> {
        override val _type: KClass<FileEvent> = FileEvent::class
        
        @Suppress("UNCHECKED_CAST")
        override fun read(ctx: SerializationCtx, buffer: AbstractBuffer): FileEvent {
            val fileName = buffer.readString()
            val data = buffer.readString()
            val data_array = buffer.readCharArray()
            val eventType = buffer.readString()
            return FileEvent(fileName, data, data_array, eventType)
        }
        
        override fun write(ctx: SerializationCtx, buffer: AbstractBuffer, value: FileEvent) {
            buffer.writeString(value.fileName)
            buffer.writeString(value.data)
            buffer.writeCharArray(value.data_array)
            buffer.writeString(value.eventType)
        }
        
    }
    //fields
    //initializer
    //secondary constructor
    //equals trait
    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (other == null || other::class != this::class) return false
        
        other as FileEvent
        
        if (fileName != other.fileName) return false
        if (data != other.data) return false
        if (!(data_array contentEquals other.data_array)) return false
        if (eventType != other.eventType) return false
        
        return true
    }
    //hash code trait
    override fun hashCode(): Int {
        var __r = 0
        __r = __r*31 + fileName.hashCode()
        __r = __r*31 + data.hashCode()
        __r = __r*31 + data_array.contentHashCode()
        __r = __r*31 + eventType.hashCode()
        return __r
    }
    //pretty print
    override fun print(printer: PrettyPrinter) {
        printer.println("FileEvent (")
        printer.indent {
            print("fileName = "); fileName.print(printer); println()
            print("data = "); data.print(printer); println()
            print("data_array = "); data_array.print(printer); println()
            print("eventType = "); eventType.print(printer); println()
        }
        printer.print(")")
    }
}


enum class MyEnum {
    FirstValue,
    SecondVaddlue;
    
    companion object { val marshaller = FrameworkMarshallers.enum<MyEnum>() }
}
