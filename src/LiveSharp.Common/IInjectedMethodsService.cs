using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiveSharp.Common
{
    public interface IInjectedMethodsService
    {
        bool FindMethodByIdentifier(string methodIdentifier, out InjectedMethodInfo result);
        Task<IReadOnlyList<InjectedMethodInfo>> Search(string term);
        bool GetMethod(int methodId, out InjectedMethodInfo result);
        IReadOnlyList<InjectedMethodInfo> GetMethods();
    }
}