﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FilterSchemeSerializerModifier.cs" company="Orcomp development team">
//   Copyright (c) 2008 - 2014 Orcomp development team. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.FilterBuilder.Runtime.Serialization
{
    using System;
    using System.Reflection;
    using Catel;
    using Catel.Reflection;
    using Catel.Runtime.Serialization;
    using Orc.FilterBuilder.Models;
    using Orc.FilterBuilder.Services;

    public class PropertyExpressionSerializerModifier : SerializerModifierBase<PropertyExpression>
    {
        private const string Separator = "||";

        private readonly IReflectionService _reflectionService;

        public PropertyExpressionSerializerModifier(IReflectionService reflectionService)
        {
            Argument.IsNotNull(() => reflectionService);

            _reflectionService = reflectionService;
        }

        public override void SerializeMember(ISerializationContext context, MemberValue memberValue)
        {
            if (string.Equals(memberValue.Name, "Property"))
            {
                var propertyInfo = memberValue.Value as IPropertyMetadata;
                if (propertyInfo != null)
                {
                    memberValue.Value = string.Format("{0}{1}{2}", propertyInfo.OwnerType.FullName, Separator, propertyInfo.Name);
                }
            }
        }

        public override void DeserializeMember(ISerializationContext context, MemberValue memberValue)
        {
            if (string.Equals(memberValue.Name, "Property"))
            {
                var propertyName = memberValue.Value as string;
                if (propertyName != null)
                {
                    var splittedString = propertyName.Split(new[] {Separator}, StringSplitOptions.RemoveEmptyEntries);
                    if (splittedString.Length == 2)
                    {
                        var type = TypeCache.GetType(splittedString[0]);
                        if (type != null)
                        {
                            var typeProperties = _reflectionService.GetInstanceProperties(type);
                            memberValue.Value = typeProperties.GetProperty(splittedString[1]);
                        }
                    }
                }
            }
        }
    }
}