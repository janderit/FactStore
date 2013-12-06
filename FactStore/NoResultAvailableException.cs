using System;

namespace JIT.FactStore
{
    public class NoResultAvailableException : Exception
    {
        public NoResultAvailableException(Type result_type, Exception innerException) : base("No data of type "+result_type.FullName+" available due to error: '"+innerException.Message+"'", innerException)
        {
        }
    }
}