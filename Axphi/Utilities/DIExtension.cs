using System;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;

namespace Axphi.Utilities
{
    // 继承 MarkupExtension，这是让 XAML 认识它的关键
    public class DIExtension : MarkupExtension
    {
        public Type Type { get; set; }

        public DIExtension(Type type)
        {
            Type = type;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // 如果没传类型，直接报错
            if (Type == null)
                throw new InvalidOperationException("The Type property must be set.");

            // 核心逻辑：直接从全局的 DI 容器中解析这个类型
            return App.Services.GetRequiredService(Type);
        }
    }
}