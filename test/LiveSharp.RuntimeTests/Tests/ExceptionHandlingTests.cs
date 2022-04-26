using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class ExceptionHandlingTests : TestsBase
    {
        public void Test0()
        {
            var flag = false;
        
            try {
        
            } finally {
                flag = true;
            }
        
            AssertEqual(flag, true);
        }
        
        public void Test1()
        {
            var flag0 = false;
            var flag1 = false;
        
            try {
                flag0 = true;
            } finally {
                flag1 = true;
            }
        
            AssertEqual(flag0, true);
            AssertEqual(flag1, true);
        }
        
        public void Test2()
        {
            var flag0 = false;
        
            try {
                throw new TestException();
            } catch {
                flag0 = true;
            }
        
            AssertEqual(flag0, true);
        }
        
        public void Test3()
        {
            var flag0 = false;
        
            try {
                throw new TestException();
            } catch (TestException) {
                flag0 = true;
            }
        
            AssertEqual(flag0, true);
        }
        
        public void Test4()
        {
            var flag0 = false;
        
            try {
                throw new TestException();
            } catch (TestException e) {
                Debug.WriteLine("TestException executed");
                flag0 = e != null;
            } catch (Exception e) {
                Debug.WriteLine("Exception executed");
            }
        
            AssertEqual(flag0, true);
        }
        
        public void Test5()
        {
            var b = true;
            var s = b ? "a" : throw new TestException();
            
            AssertEqual(s, "a");
        }
        
        public void Test6()
        {
            var flag = false;
        
            try {
                try {
                    throw new TestException();
                } catch(TestException) {
                    throw;
                }
            } catch (Exception e) {
                flag = e is TestException;
            }
        
            AssertEqual(flag, true);
        }
        
        public void Test7()
        {
            var flag = false;
            var filter = false;
        
            try {
            } catch when (filter == true) {
                flag = true;
            }
        
            AssertEqual(flag, false);
        }
        
        public void Test8()
        {
            var flag = false;
            var filter = false;

            try {
                throw new TestException();
            } catch (TestException e) when (filter == false) {
                flag = true && e != null;
            }

            AssertEqual(flag, true);
        }
        
        public void Test9()
        {
            var finallyCalled = false;
            try {
            }
            catch (TestException e) {
            } finally {
                finallyCalled = true;
            }
            
            Assert(finallyCalled);
        }
        
        public void Test10()
        {
            var finallyCalled = false;
            try {
            }
            catch (TestException e) {
            } finally {
                finallyCalled = true;
            }
            
            Assert(finallyCalled);
        }
        
        public void Test20()
        {
            var finallyCalled = false;
            try {
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
                finallyCalled = false;
            }
            catch (TestException e) {
            } finally {
                finallyCalled = true;
            }
            
            Assert(finallyCalled);
        }

        class TestException : Exception
        {}
    }
}
