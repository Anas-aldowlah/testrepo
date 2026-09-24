using System;
using Microsoft.AspNetCore.Mvc;

namespace YAGOT_2._0.Filters;

/// <summary>
/// سمة الفحص اللحظي للصلاحيات على مستوى الطلب (Per-Request Capability Guard).
/// يتم تنفيذها قبل دخول أي Action وتمنع الوصول فوراً إذا كانت الميزة معطلة.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireCapabilityAttribute : TypeFilterAttribute
{
    public RequireCapabilityAttribute(string capabilityCode)
        : base(typeof(RequireCapabilityFilter))
    {
        Arguments = [capabilityCode];
    }
}
