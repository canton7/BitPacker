using System;
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
        private readonly Type type;
        private readonly Expression value;
        private Endianness? endianness;
        private List<ObjectDetails> properties;
        private readonly BitPackerMemberAttribute propertyAttribute;
        private readonly ObjectDetails elementObjectDetails;
        private readonly ObjectDetails enumEquivalentObjectDetails;

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
        
        public IReadOnlyList<ObjectDetails> Properties
        {
            get { return this.properties; }
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

        public ObjectDetails ElementObjectDetails
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

        public ObjectDetails EnumEquivalentObjectDetails
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
                this.elementObjectDetails = new ObjectDetails(this.ElementType, null, this.propertyAttribute, this.Endianness);

            if (this.IsEnum)
            {
                this.enumEquivalentObjectDetails = new ObjectDetails(this.EnumEquivalentType, this.value, this.propertyAttribute, this.Endianness);
                this.CheckEnum();
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
                                  select new ObjectDetails(property.PropertyType, Expression.MakeMemberAccess(this.value, property), propertyAttribute, this.Endianness)).ToList();

                foreach (var property in properties)
                {
                    property.Discover();
                }

                this.properties = properties;
            }

            if (this.elementObjectDetails != null)
                this.elementObjectDetails.Discover();

            if (this.enumEquivalentObjectDetails != null)
                this.enumEquivalentObjectDetails.Discover();
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

        public IEnumerable<ObjectDetails> RecursiveFlatProperties()
        {
            if (this.properties == null)
                return new[] { this };
            else
                return new[] { this }.Concat(this.properties.SelectMany(x => x.RecursiveFlatProperties()));
        }
    }
}
