using Microsoft.Extensions.Options;
using System;

namespace OcrSharp.Api.Setup
{
    interface IWritableOptions<out T> : IOptions<T> where T : class, new()
    {
        void Update(Action<T> applyChanges);
    }
}
