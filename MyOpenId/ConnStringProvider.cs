using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class ConnStringProvider : IConnStringProvider
    {
        private readonly Func<string> getConnStringFunc;

        public ConnStringProvider(Func<string> getConnStringFunc) 
        {
            this.getConnStringFunc = getConnStringFunc;
        }

        public string GetConnectionString()
        {
            return this.getConnStringFunc();
        }
    }
}
