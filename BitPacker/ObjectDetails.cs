using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class ObjectDetails
    {
        protected readonly Type type;
        protected Endianness? endianness;
        protected IReadOnlyList<PropertyObjectDetails> properties;
        protected IReadOnlyDictionary<string, PropertyObjectDetails> lengthFields;
        protected readonly BitPackerObjectAttribute objectAttribute;
        protected readonly BitPackerMemberAttribute propertyAttribute;
        protected readonly string lengthKey;
        protected readonly int length;
        protected readonly EnumerableElementObjectDetails elementObjectDetails;
        protected readonly EnumObjectDetails enumEquivalentObjectDetails;
        protected readonly Type customSerializer;
        protected readonly Type customDeserializer;

        public Type Type
        {
            get { return this.type; }
        }

        public Endianness Endianness
        {
            get { return this.endianness.GetValueOrDefault(Endianness.BigEndian); }
        }

        public string LengthKey
        {
            get { return this.lengthKey; }
        }
        
        public IReadOnlyList<PropertyObjectDetails> Properties
        {
            get { return this.properties; }
        }

        public IReadOnlyDictionary<string, PropertyObjectDetails> LengthFields
        {
            get
            {
                if (!this.IsCustomType)
                    throw new InvalidOperationException("Not custom type");
                return this.lengthFields;
            }
        }

        public bool IsCustomType
        {
            get { return this.properties != null; }
        }

        public bool IsEnumerable
        {
            get { return this.Type.IsArray || this.Type.Implements(typeof(IEnumerable<>)); }
        }

        public Type ElementType
        {
            get
            {
                if (!this.IsEnumerable)
                    throw new InvalidOperationException("Not Enumerable");
                return this.Type.IsArray ? this.Type.GetElementType() : this.Type.GetGenericArguments()[0];
            }
        }

        public EnumerableElementObjectDetails ElementObjectDetails
        {
            get
            {
                if (!this.IsEnumerable)
                    throw new InvalidOperationException("Not Enumerable");
                return this.elementObjectDetails;
            }
        }

        public int EnumerableLength
        {
            get { return this.length; }
        }

        public bool IsEnum
        {
            get { return typeof(Enum).IsAssignableFrom(this.Type); }
        }

        public Type EnumEquivalentType
        {
            get
            {
                if (!this.IsEnum)
                    throw new InvalidOperationException("Not Enum");
                return this.propertyAttribute.EnumType == null ? typeof(int) : this.propertyAttribute.EnumType;
            }
        }

        public bool Serialize
        {
            get { return this.propertyAttribute.SerializeInternal; }
        }

        public Type CustomSerializer
        {
            get { return this.customSerializer; }
        }

        public Type CustomDeserializer
        {
            get { return this.customDeserializer; }
        }

        public EnumObjectDetails EnumEquivalentObjectDetails
        {
            get
            {
                if (!this.IsEnum)
                    throw new InvalidOperationException("Not Enum");
                return this.enumEquivalentObjectDetails;
            }
        }

        public ObjectDetails(Type type, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null, bool isAttributeCascaded = false)
        {
            this.type = type;
            this.objectAttribute = this.type.GetCustomAttribute<BitPackerObjectAttribute>();
            this.endianness = endianness;
            this.propertyAttribute = propertyAttribute;

            if (this.endianness == null && this.objectAttribute != null)
                this.endianness = this.objectAttribute.Endianness;

            this.customSerializer = propertyAttribute.CustomSerializer;
            if (this.customSerializer == null && this.objectAttribute != null)
                this.customSerializer = this.objectAttribute.CustomSerializer;

            this.customDeserializer = propertyAttribute.CustomDeserializer;
            if (this.customDeserializer == null && this.objectAttribute != null)
                this.customDeserializer = this.objectAttribute.CustomDeserializer;

            if (this.IsEnum)
            {
                this.enumEquivalentObjectDetails = new EnumObjectDetails(this.EnumEquivalentType, this.propertyAttribute, this.Endianness);
                this.CheckEnum();
            }

            var arrayAttribute = propertyAttribute as BitPackerArrayAttribute;
            if (arrayAttribute != null && !isAttributeCascaded)
            {
                if (!this.IsEnumerable)
                    throw new Exception("BitPackerArray can only be applied to properties which are arrays or IEnumerable<T>");

                this.elementObjectDetails = new EnumerableElementObjectDetails(this.ElementType, this.propertyAttribute, this.Endianness);
                this.length = arrayAttribute.Length;
                this.lengthKey = arrayAttribute.LengthKey;
            }
            else if (this.IsEnumerable)
            {
                throw new Exception("Arrays or IEnumerable<T> properties must be decorated with BitPackerArray, not BitPackerMember");
            }

            var arrayLengthAttribute = propertyAttribute as BitPackerArrayLengthAttribute;
            if (arrayLengthAttribute != null)
            {
                if (!PrimitiveTypes.IsPrimitive(this.Type) || !PrimitiveTypes.Types[this.Type].IsIntegral)
                    throw new Exception("Properties decorated with BitPackerArrayLength must be integer");

                this.lengthKey = arrayLengthAttribute.LengthKey;
            }
        }

        private void CheckEnum()
        {
            // Check that no value in the enum exceeds the given size
            var length = PrimitiveTypes.Types[this.EnumEquivalentType].Size;
            var maxVal = Math.Pow(2, length * 8);
            // Can't use linq, as it's an non-generic IEnumerable of value types
            foreach (var enumVal in Enum.GetValues(this.Type))
            {
                if ((int)enumVal > maxVal)
                    throw new Exception(String.Format("Enum type {0} has a size of {1} bytes, but has a member which is greater than this", this.Type, length));
            }
        }

        public void Discover()
        {
            if (this.objectAttribute != null)
            {
                var allProperties = (from property in this.type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  let propertyAttribute = property.GetCustomAttribute<BitPackerMemberAttribute>(false)
                                  where propertyAttribute != null
                                  orderby propertyAttribute.Order
                                  select new PropertyObjectDetails(property, propertyAttribute, this.Endianness)).ToList();

                var properties = allProperties.Where(x => x.propertyAttribute.SerializeInternal).ToList();

                this.lengthFields = allProperties.Where(x => x.propertyAttribute is BitPackerArrayLengthAttribute).ToDictionary(x => x.LengthKey, x => x);

                foreach (var property in properties)
                {
                    property.Discover();
                }

                this.properties = properties.AsReadOnly();
            }

            if (this.elementObjectDetails != null)
                this.elementObjectDetails.Discover();

            if (this.enumEquivalentObjectDetails != null)
                this.enumEquivalentObjectDetails.Discover();
        }

        public IEnumerable<PropertyObjectDetailsWithAccess> RecursiveFlatPropertyAccess(Expression subject)
        {
            if (this.properties == null)
                return Enumerable.Empty<PropertyObjectDetailsWithAccess>();
            return this.properties.SelectMany(x =>
            {
                var property = x.AccessExpression(subject);
                return new[] { new PropertyObjectDetailsWithAccess(x, property) }.Concat(x.RecursiveFlatPropertyAccess(property));
            });
        }
    }

    internal class PropertyObjectDetails : ObjectDetails
    {
        public PropertyInfo PropertyInfo { get; private set; }

        public PropertyObjectDetails(PropertyInfo propertyInfo, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(propertyInfo.PropertyType, propertyAttribute, endianness)
        {
            this.PropertyInfo = propertyInfo;
        }

        public Expression AccessExpression(Expression parent)
        {
            return Expression.MakeMemberAccess(parent, this.PropertyInfo);
        }
    }

    internal class EnumObjectDetails : ObjectDetails
    {
        public EnumObjectDetails(Type type, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(type, propertyAttribute, endianness)
        { }

        
    }

    internal class EnumerableElementObjectDetails : ObjectDetails
    {
        public EnumerableElementObjectDetails(Type type, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(type, propertyAttribute, endianness, true)
        { }

        public Expression AssignExpression(ParameterExpression parent, Expression index, Expression value)
        {
            if (parent.Type.IsArray)
            {
                return Expression.Assign(Expression.ArrayAccess(parent, index), value);
            }
            if (parent.Type.Implements(typeof(IList<>)))
            {
                var method = parent.Type.GetMethod("Add");
                // We ignore the index for this one
                return Expression.Call(parent, method, value);
            }
            throw new InvalidOperationException("Can't assign to member of something which isn't an array or IList");
        }
    }

    internal class PropertyObjectDetailsWithAccess
    {
        public PropertyObjectDetails ObjectDetails { get; private set; }
        public Expression Value { get; private set; }

        public PropertyObjectDetailsWithAccess(PropertyObjectDetails objectDetails, Expression value)
        {
            this.ObjectDetails = objectDetails;
            this.Value = value;
        }
    }
}
