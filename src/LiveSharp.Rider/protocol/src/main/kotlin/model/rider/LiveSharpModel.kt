package model.rider

import com.jetbrains.rider.model.nova.ide.SolutionModel
import com.jetbrains.rd.generator.nova.*
import com.jetbrains.rd.generator.nova.PredefinedType.*
//import com.jetbrains.rd.generator.nova.csharp.CSharp50Generator
//import com.jetbrains.rd.generator.nova.kotlin.Kotlin11Generator

@Suppress("unused")
object LiveSharpModel : Ext(SolutionModel.Solution) {

    val MyEnum = enum {
        +"FirstValue"
        +"SecondVaddlue"
    }

    val fileEvent = structdef {
        field("fileName", string)
        field("data", string)
        field("data_array", array(char))
        field("eventType", string)
    }

    init {
        //setting(CSharp50Generator.Namespace, "LiveSharp.ReSharperRider.Model")
        //setting(Kotlin11Generator.Namespace, "com.jetbrains.rider.livesharp.model")

        property("myEnum", MyEnum.nullable)

        source("fileEvent", fileEvent)
        //signal("fileEvent", fileEvent)
    }
}