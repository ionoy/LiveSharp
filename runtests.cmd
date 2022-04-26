dotnet build LiveSharp-testonly.sln --no-incremental
pushd src\LiveSharp.CodedUITest\bin\Debug\netcoreapp3.1
dotnet LiveSharp.CodedUiTest.dll
popd