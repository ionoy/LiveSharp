# LiveSharp
Original hot reload solution for .NET platform. This project has mostly been superceded by a built-in hot reload in .NET 6. However, there are still many issues with the native hot reload, that's why I decided to open LiveSharp to the public

# How to build

* Open and build LiveSharp.Build.sln
* Close LiveSharp.Build.sln
* Open and build LiveSharp.sln

# How to run locally

* Go to `src\LiveSharp.Server` and `dotnet run`
* Open your project and paste the following lines 
```
    <ItemGroup>
        <Reference Include="{PATH_TO_LIVESHARP}\livesharp\build\livesharp.dll" />
    </ItemGroup>
    
    <Import Project="{PATH_TO_LIVESHARP}\livesharp\build\livesharp.targets" />
```    
* Replace `{PATH_TO_LIVESHARP}` with the actual path to the repo
* Run your project





