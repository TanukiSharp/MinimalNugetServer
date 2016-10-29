using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MinimalNugetServer
{
    public abstract class RequestProcessorBase
    {
        public MasterData MasterData { get; private set; }

        public virtual void Initialize(MasterData masterData)
        {
            if (masterData == null)
                throw new ArgumentNullException(nameof(masterData));

            MasterData = masterData;
        }

        public abstract PathString Segment { get; }
        public abstract Task ProcessRequest(HttpContext context);
    }
}
