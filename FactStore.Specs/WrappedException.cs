using System;

namespace FactStore.Specs
{
    public class WrappedException : Exception
    {
        public WrappedException(Exception innerException) : base("Exception during test run: "+innerException.Message, innerException)
        {
        }
    }
}