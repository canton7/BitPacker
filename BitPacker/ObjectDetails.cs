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
        public static readonly Encoding[] NullTerminatedEncodings = new[] { Encoding.ASCII, Encoding.UTF8 };
        protected readonly Type type;
        protected readonly string debugName;
        protected Endianness? endianness;
        protected IReadOnlyList<PropertyObjectDetails> properties;
        protected IReadOnlyDictionary<string, PropertyObjectDetails> lengthFields;
        protected IReadOnlyDictionary<string, PropertyObjectDetails> variableLengthArrays;
        protected readonly BitPackerObjectAttribute objectAttribute;
        protected readonly BitPackerMemberAttribute propertyAttribute;
        protected readonly string lengthKey;
        protected readonly int length;
        protected readonly Encoding encoding;
        protected readonly bool nullTerminated;
        protected readonly EnumerableElementObjectDetails elementObjectDetails;
        protected readonly ObjectDetails enumEquivalentObjectDetails;
        protected readonly ObjectDetails booleanEquivalentObjectDetails;
        protected readonly Type customSerializer;
        protected readonly Type customDeserializer;
        protected readonly Type equivalentType;
        protected readonly int? bitWidth;
        protected readonly bool padContainerAfter;
        protected readonly bool isPrimitiveType;
        protected readonly IPrimitiveTypeInfo primitiveTypeInfo;

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

        public IReadOnlyDictionary<string, PropertyObjectDetails> VariableLengthArrays
        {
            get
            {
                if (!this.IsCustomType)
                    throw new InvalidOperationException("Not custom type");
                return this.variableLengthArrays;
            }
        }

        public bool IsCustomType
        {
            get { return this.properties != null; }
        }

        public bool IsString
        {
            get { return this.Type == typeof(string); }
        }

        public Encoding Encoding
        {
            get
            {
                if (!this.IsString)
                    throw new InvalidOperationException("Not a string");
                return this.encoding;
            }
        }

        public bool NullTerminated
        {
            get
            {
                if (!this.IsString)
                    throw new InvalidOperationException("Not a string");
                return this.nullTerminated;
            }
        }

        public bool IsBoolean
        {
            get { return this.Type == typeof(bool); }
        }

        public bool IsEnumerable
        {
            get { return this.IsString || this.Type.IsArray || this.Type.Implements(typeof(IEnumerable<>)); }
        }

        public Type ElementType
        {
            get
            {
                if (!this.IsEnumerable)
                    throw new InvalidOperationException("Not Enumerable");
                if (this.IsString)
                    return typeof(byte);
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

        public bool Serialize
        {
            get { return this.propertyAttribute.SerializeInternal; }
        }

        public int Order
        {
            get { return this.propertyAttribute.Order; }
        }

        public Type CustomSerializer
        {
            get { return this.customSerializer; }
        }

        public Type CustomDeserializer
        {
            get { return this.customDeserializer; }
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

        public ObjectDetails BooleanEquivalentObjectDetails
        {
            get
            {
                if (!this.IsBoolean)
                    throw new InvalidOperationException("Not Bool");
                return this.booleanEquivalentObjectDetails;
            }
        }

        public int? BitWidth
        {
            get { return this.bitWidth; }
        }

        public bool PadContainerAfter
        {
            get { return this.padContainerAfter; }
        }

        public bool IsPrimitiveType
        {
            get { return this.isPrimitiveType; }
        }

        public bool IsLengthField
        {
            get { return this.propertyAttribute is BitPackerLengthKeyAttribute; }
        }

        public IPrimitiveTypeInfo PrimitiveTypeInfo
        {
            get
            {
                if (!this.isPrimitiveType)
                    throw new InvalidOperationException("Not a primitive type");
                return this.primitiveTypeInfo;
            }
        }

        public ObjectDetails(Type type, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null, bool isAttributeCascaded = false)
            : this(type, type.Description(), propertyAttribute, endianness, isAttributeCascaded)
        { }

        public ObjectDetails(Type type, string debugName, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null, bool isAttributeCascaded = false)
        {
            this.type = type;
            this.debugName = debugName;
            this.objectAttribute = this.type.GetCustomAttribute<BitPackerObjectAttribute>();
            this.endianness = endianness;
            this.propertyAttribute = propertyAttribute;
            this.isPrimitiveType = PrimitiveTypes.IsPrimitive(this.type);
            if (this.isPrimitiveType)
                this.primitiveTypeInfo = PrimitiveTypes.Types[this.type];

            if (this.endianness == null && this.objectAttribute != null)
                this.endianness = this.objectAttribute.Endianness;

            if (propertyAttribute.NullableEndianness != null)
                this.endianness = propertyAttribute.NullableEndianness.Value;

            this.customSerializer = propertyAttribute.Serializer;
            if (this.customSerializer == null && this.objectAttribute != null)
                this.customSerializer = this.objectAttribute.Serializer;

            this.customDeserializer = propertyAttribute.Deserializer;
            if (this.customDeserializer == null && this.objectAttribute != null)
                this.customDeserializer = this.objectAttribute.Deserializer;

            var stringAttribute = propertyAttribute as BitPackerStringAttribute;
            if (stringAttribute != null && !isAttributeCascaded)
            {
                if (!this.IsString)
                    throw new InvalidAttributeException("BitPackerString can only be applied to properties which are strings", this.debugName);

                this.elementObjectDetails = new EnumerableElementObjectDetails(this.ElementType, propertyAttribute);
                this.length = stringAttribute.Length;
                this.lengthKey = stringAttribute.LengthKey;
                this.encoding = Encoding.GetEncoding(stringAttribute.Encoding);
                this.nullTerminated = stringAttribute.NullTerminated;

                if (stringAttribute.NullTerminated && !NullTerminatedEncodings.Contains(this.encoding))
                    throw new InvalidAttributeException(String.Format("The only string encodings which may be null-terminated are {0}", String.Join(", ", NullTerminatedEncodings.Select(x => x.EncodingName))), this.debugName);
            }
            else if (this.IsString)
            {
                throw new InvalidAttributeException("String properties must be decorated with BitPackerString", this.debugName);
            }

            var arrayAttribute = propertyAttribute as BitPackerArrayAttribute;
            if (arrayAttribute != null && !isAttributeCascaded)
            {
                if (!this.IsEnumerable && !this.IsString)
                    throw new InvalidAttributeException("BitPackerArray can only be applied to properties which are arrays or IEnumerable<T>", this.debugName);

                this.elementObjectDetails = new EnumerableElementObjectDetails(this.ElementType, propertyAttribute, this.Endianness);
                this.length = arrayAttribute.Length;
                this.lengthKey = arrayAttribute.LengthKey;
            }
            else if (this.IsEnumerable && !this.IsString)
            {
                throw new InvalidAttributeException("Arrays or IEnumerable<T> properties must be decorated with BitPackerArray, not BitPackerMember", this.debugName);
            }

            // Check has to happen before BitPackerIntegerAttribute
            var booleanAttribute = propertyAttribute as BitPackerBooleanAttribute;
            if (booleanAttribute != null && !isAttributeCascaded)
            {
                if (!this.IsBoolean)
                    throw new InvalidAttributeException("Properties decorated with BitPackerBoolean must be booleans", this.debugName);

                var equivalentType = booleanAttribute.Type;
                if (equivalentType == typeof(bool))
                    equivalentType = null;
                if (equivalentType != null)
                    this.EnsureTypeIsInteger(booleanAttribute.Type);
                this.equivalentType = booleanAttribute.Type;
            }
            if (this.IsBoolean)
            {
                this.equivalentType = this.equivalentType ?? typeof(int);
                this.booleanEquivalentObjectDetails = new ObjectDetails(this.equivalentType, propertyAttribute, this.Endianness, true);
            }

            // Check has to happen before BitPackerIntegerAttribute
            var enumAttribute = propertyAttribute as BitPackerEnumAttribute;
            if (enumAttribute != null && !isAttributeCascaded)
            {
                if (!this.Type.IsEnum)
                    throw new Exception("Properties decorated with BitPackerEnum must be enums");

                this.EnsureTypeIsInteger(enumAttribute.Type);
                this.equivalentType = enumAttribute.Type;
            }
            if (this.IsEnum)
            {
                var enumType = this.equivalentType ?? Enum.GetUnderlyingType(this.Type);
                this.enumEquivalentObjectDetails = new ObjectDetails(enumType, propertyAttribute, this.Endianness, true);
                this.CheckEnum(enumType);
            }

            var integerAttribute = propertyAttribute as BitPackerIntegerAttribute;
            if (integerAttribute != null)
            {
                var typeToCheck = this.equivalentType ?? this.Type;
                IPrimitiveTypeInfo primitiveTypeInfo;
                if (!PrimitiveTypes.Types.TryGetValue(typeToCheck, out primitiveTypeInfo) || !primitiveTypeInfo.IsIntegral)
                    throw new Exception("Properties decorated with BitPackerInteger or BitPackerArrayLength must be integral");

                this.bitWidth = integerAttribute.NullableBitWidth;
                if (this.bitWidth.HasValue && this.bitWidth.Value <= 0)
                    throw new Exception("Bit Width must be > 0");
                this.padContainerAfter = integerAttribute.PadContainerAfter;
            }

            var arrayLengthAttribute = propertyAttribute as BitPackerLengthKeyAttribute;
            if (arrayLengthAttribute != null)
            {
                // Type-checking already done by BitPackerIntegerAttribute
                this.lengthKey = arrayLengthAttribute.LengthKey;
            }
        }

        private void CheckEnum(Type enumType)
        {
            var underlyingType = Enum.GetUnderlyingType(this.Type);
            var underlyingTypeIsSigned = PrimitiveTypes.Types[underlyingType].IsSigned;

            var typeInfo = PrimitiveTypes.Types[enumType];

            foreach (var enumVal in Enum.GetValues(this.Type))
            {
                if (underlyingTypeIsSigned)
                {
                    var value = Convert.ToInt64(enumVal);
                    if (value < typeInfo.MinValue || (value > 0 && (ulong)value > typeInfo.MaxValue))
                        throw new Exception(String.Format("Enum type {0} has a size of {1} bytes, but has a member '{2}' which does not fit in this", this.Type, typeInfo.Size, enumVal));
                }
                else
                {
                    if (Convert.ToUInt64(enumVal) > typeInfo.MaxValue)
                        throw new Exception(String.Format("Enum type {0} has a size of {1} bytes, but has a member '{2}' which does not fit in this", this.Type, typeInfo.Size, enumVal));
                }
            }
        }

        private void EnsureTypeIsInteger(Type type)
        {
            IPrimitiveTypeInfo primitiveTypeInfo;
            if (!PrimitiveTypes.TryGetValue(type, out primitiveTypeInfo) || !primitiveTypeInfo.IsIntegral)
                throw new InvalidEquivalentTypeException(String.Format("Type {0} must be an integer", type), this.debugName);
        }

        public void Discover()
        {
            if (this.objectAttribute != null)
            {
                var allProperties = (from property in this.type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  let propertyAttribute = property.GetCustomAttribute<BitPackerMemberAttribute>(false)
                                  where propertyAttribute != null
                                  orderby propertyAttribute.Order
                                  select new PropertyObjectDetails(this.type, property, propertyAttribute, this.Endianness)).ToList();

                var properties = allProperties.Where(x => x.propertyAttribute.SerializeInternal).ToList();
                foreach (var property in properties)
                {
                    property.Discover();
                }

                this.properties = properties.AsReadOnly();

                var lengthFieldGroups = allProperties.Where(x => x.IsLengthField).GroupBy(x => x.LengthKey).ToArray();
                var firstLengthFieldDuplicate = lengthFieldGroups.FirstOrDefault(x => x.Count() > 1);
                if (firstLengthFieldDuplicate != null)
                    throw new InvalidArraySetupException(String.Format("Found more than one property with length key '{0}'", firstLengthFieldDuplicate.Key));

                this.lengthFields = lengthFieldGroups.ToDictionary(x => x.Key, x => x.Single());

                // TODO This could be neater...
                var variableLengthArrayGroups = (from property in allProperties
                                                 where property.IsEnumerable && property.LengthKey != null
                                                 group property by property.LengthKey).ToArray();

                var firstVariableLengthArrayDuplicate = variableLengthArrayGroups.FirstOrDefault(x => x.Count() > 1);
                if (firstVariableLengthArrayDuplicate != null)
                    throw new InvalidArraySetupException(String.Format("Found more than one variable-length array with length key '{0}'", firstLengthFieldDuplicate.Key));

                this.variableLengthArrays = variableLengthArrayGroups.ToDictionary(x => x.Key, x => x.Single());
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

        public PropertyObjectDetails(Type parentType, PropertyInfo propertyInfo, BitPackerMemberAttribute propertyAttribute, Endianness? endianness = null)
            : base(propertyInfo.PropertyType, String.Format("{0}.{1}", parentType.Description(), propertyInfo.Name), propertyAttribute, endianness)
        {
            this.PropertyInfo = propertyInfo;
        }

        public Expression AccessExpression(Expression parent)
        {
            return Expression.MakeMemberAccess(parent, this.PropertyInfo);
        }
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
                // Array indexes have to be int32
                return Expression.Assign(Expression.ArrayAccess(parent, Expression.Convert(index, typeof(int))), value);
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
