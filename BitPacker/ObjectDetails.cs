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
        protected readonly Expression value;
        protected Endianness? endianness;
        protected IReadOnlyList<PropertyObjectDetails> properties;
        protected IReadOnlyDictionary<string, PropertyObjectDetails> lengthFields;
        protected readonly BitPackerMemberAttribute propertyAttribute;
        protected readonly EnumerableElementObjectDetails elementObjectDetails;
        protected readonly EnumObjectDetails enumEquivalentObjectDetails;

        public Type Type
        {
            get { return this.type; }
        }

        public Expression Value
        {
            get { return this.value; }
        }

        public Endianness Endianness
        {
            get { return this.endianness.GetValueOrDefault(Endianness.BigEndian); }
        }

        public string LengthKey
        {
            get { return this.propertyAttribute.LengthKey; }
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
            get { return this.propertyAttribute.Length; }
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

        public EnumObjectDetails EnumEquivalentObjectDetails
        {
            get
            {
                if (!this.IsEnum)
                    throw new InvalidOperationException("Not Enum");
                return this.enumEquivalentObjectDetails;
            }
        }

        public ObjectDetails(Type type, Expression value, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
        {
            this.type = type;
            this.value = value;
            this.endianness = endianness;
            this.propertyAttribute = propertyAttribute;

            if (this.IsEnumerable)
                this.elementObjectDetails = new EnumerableElementObjectDetails(this.ElementType, Expression.Variable(this.ElementType, "elementVar"), this.propertyAttribute, this.Endianness);

            if (this.IsEnum)
            {
                this.enumEquivalentObjectDetails = new EnumObjectDetails(this.EnumEquivalentType, this.value, this.propertyAttribute, this.Endianness);
                this.CheckEnum();
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
            var attribute = this.type.GetCustomAttribute<BitPackerObjectAttribute>();
            if (attribute != null)
            {
                this.endianness = this.endianness ?? attribute.Endianness;

                var properties = (from property in this.type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  let propertyAttribute = property.GetCustomAttribute<BitPackerMemberAttribute>(false)
                                  where propertyAttribute != null
                                  orderby propertyAttribute.Order
                                  select new PropertyObjectDetails(property, Expression.MakeMemberAccess(this.value, property), propertyAttribute, this.Endianness)).ToList();

                // TODO: Support length fields which aren't marked as members
                this.lengthFields = properties.Where(x => x.LengthKey != null && PrimitiveTypes.IsPrimitive(x.Type) && PrimitiveTypes.Types[x.Type].IsIntegral)
                    .ToDictionary(x => x.LengthKey, x => x);

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

        

        public IEnumerable<ObjectDetails> RecursiveFlatProperties()
        {
            if (this.properties == null)
                return new[] { this };
            else
                return new[] { this }.Concat(this.properties.SelectMany(x => x.RecursiveFlatProperties()));
        }
    }

    internal class PropertyObjectDetails : ObjectDetails
    {
        private readonly PropertyInfo propertyInfo;

        public PropertyObjectDetails(PropertyInfo propertyInfo, Expression value, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(propertyInfo.PropertyType, value, propertyAttribute, endianness)
        {
            this.propertyInfo = propertyInfo;
        }

        public Expression AccessExpression(Expression parent)
        {
            return Expression.MakeMemberAccess(parent, this.propertyInfo);
        }
    }

    internal class EnumObjectDetails : ObjectDetails
    {
        public EnumObjectDetails(Type type, Expression value, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(type, value, propertyAttribute, endianness)
        { }

        
    }

    internal class EnumerableElementObjectDetails : ObjectDetails
    {
        public EnumerableElementObjectDetails(Type type, Expression value, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(type, value, propertyAttribute, endianness)
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
}
