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
        protected readonly BitPackerBitFieldAttribute bitFieldAttribute;
        protected readonly string lengthKey;
        protected readonly int length;
        protected readonly bool serialize;
        protected readonly int order;
        protected readonly Encoding encoding;
        protected readonly bool nullTerminated;
        protected readonly EnumerableElementObjectDetails elementObjectDetails;
        protected readonly ObjectDetails enumEquivalentObjectDetails;
        protected readonly ObjectDetails booleanEquivalentObjectDetails;
        protected readonly Type customSerializer;
        protected readonly Type customDeserializer;
        protected readonly Type equivalentType;
        protected readonly int? bitWidth;
        protected readonly bool isPrimitiveType;
        protected readonly bool isLengthField;
        protected readonly bool isChildOfBitField;
        protected readonly IPrimitiveTypeInfo primitiveTypeInfo;

        protected int bitfieldWidthBits;

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

        public bool IsBitField
        {
            get { return this.bitFieldAttribute != null; }
        }

        public bool IsChildOfBitField
        {
            get { return this.isChildOfBitField; }
        }

        public int BitfieldWidthBytes
        {
            get
            {
                if (!this.IsBitField)
                    throw new InvalidOperationException("Not a BitField");
                return this.bitfieldWidthBits;
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
            get { return this.serialize; }
        }

        public int Order
        {
            get { return this.order; }
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

        public bool IsPrimitiveType
        {
            get { return this.isPrimitiveType; }
        }

        public bool IsLengthField
        {
            get { return this.isLengthField; }
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

        public ObjectDetails(Type type, ObjectDetails parent, ImmutableStack<BitPackerMemberAttribute> propertyAttributes, Endianness? endianness = null)
            : this(type, type.Description(), parent, propertyAttributes, endianness)
        { }

        public ObjectDetails(Type type, string debugName, ObjectDetails parent, ImmutableStack<BitPackerMemberAttribute> propertyAttributes, Endianness? endianness = null)
        {
            this.type = type;
            this.debugName = debugName;

            this.objectAttribute = this.type.GetCustomAttribute<BitPackerObjectAttribute>();
            this.bitFieldAttribute = this.type.GetCustomAttribute<BitPackerBitFieldAttribute>();
            if (this.objectAttribute != null && this.bitFieldAttribute != null)
                throw new InvalidAttributeException("May not be decorated with bot BitPackerObject and BitPackerBitField", this.debugName);

            this.endianness = endianness;
            this.isPrimitiveType = PrimitiveTypes.IsPrimitive(this.type);
            if (this.isPrimitiveType)
                this.primitiveTypeInfo = PrimitiveTypes.Types[this.type];

            var propertyAttribute = propertyAttributes.PeekOrDefault();

            this.serialize = propertyAttribute?.SerializeInternal ?? parent.Serialize;
            this.order = propertyAttribute?.Order ?? parent.Order;
            this.isLengthField = propertyAttribute is BitPackerLengthKeyAttribute;
            if (this.endianness == null && this.objectAttribute != null)
                this.endianness = this.objectAttribute.Endianness;

            this.isChildOfBitField = parent != null && (parent.IsBitField || parent.IsChildOfBitField);
            this.bitWidth = parent?.BitWidth; // Overridden if necessary...

            if (propertyAttribute?.NullableEndianness != null)
                this.endianness = propertyAttribute.NullableEndianness.Value;
            else
                this.endianness = parent?.Endianness;

            this.customSerializer = propertyAttribute?.Serializer ?? parent?.CustomSerializer ?? this.objectAttribute?.Serializer;
            this.customDeserializer = propertyAttribute?.Deserializer ?? parent?.CustomDeserializer ?? this.objectAttribute?.Deserializer;

            IPrimitiveTypeInfo primitiveTypeInfo;

            if (this.bitFieldAttribute != null)
            {
                if (this.bitFieldAttribute.NullableWidthBytes == null)
                    throw new InvalidAttributeException("BitPackerBitArray must have the Width attribute set", this.debugName);
                this.bitfieldWidthBits = this.bitFieldAttribute.NullableWidthBytes.Value;
            }

            var stringAttribute = propertyAttribute as BitPackerStringAttribute;
            if (stringAttribute != null)
            {
                if (!this.IsString)
                    throw new InvalidAttributeException("BitPackerString can only be applied to properties which are strings", this.debugName);

                this.elementObjectDetails = new EnumerableElementObjectDetails(this.ElementType, this, propertyAttributes.PopOrEmpty());
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
            if (arrayAttribute != null)
            {
                if (!this.IsEnumerable && !this.IsString)
                    throw new InvalidAttributeException("BitPackerArray can only be applied to properties which are arrays or IEnumerable<T>", this.debugName);

                this.elementObjectDetails = new EnumerableElementObjectDetails(this.ElementType, this, propertyAttributes.PopOrEmpty(), this.Endianness);
                this.length = arrayAttribute.Length;
                this.lengthKey = arrayAttribute.LengthKey;

                if (parent != null && parent.IsEnumerable && this.lengthKey != null)
                    throw new InvalidAttributeException("Variable-length arrays cannot appear as array elements themselves", this.debugName);
            }
            else if (this.IsEnumerable && !this.IsString)
            {
                throw new InvalidAttributeException("Arrays or IEnumerable<T> properties must be decorated with BitPackerArray", this.debugName);
            }

            // Check has to happen before BitPackerIntegerAttribute
            var booleanAttribute = propertyAttribute as BitPackerBooleanAttribute;
            if (booleanAttribute != null)
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
            // This has to happen before the IntegerAttribute check
            if (this.IsBoolean)
            {
                this.equivalentType = this.equivalentType ?? typeof(byte);
            }

            // Check has to happen before BitPackerIntegerAttribute
            var enumAttribute = propertyAttribute as BitPackerEnumAttribute;
            if (enumAttribute != null)
            {
                if (!this.Type.IsEnum)
                    throw new Exception("Properties decorated with BitPackerEnum must be enums");

                this.EnsureTypeIsInteger(enumAttribute.Type);
                this.equivalentType = enumAttribute.Type;
            }
            if (this.IsEnum)
            {
                var enumType = this.equivalentType ?? Enum.GetUnderlyingType(this.Type);
                this.enumEquivalentObjectDetails = new ObjectDetails(enumType, this, propertyAttributes.PopOrEmpty(), this.Endianness);
                this.CheckEnum(enumType);
            }

            var integerAttribute = propertyAttribute as BitPackerIntegerAttribute;
            if (integerAttribute != null)
            {
                var typeToCheck = this.equivalentType ?? this.Type;
                if (!PrimitiveTypes.Types.TryGetValue(typeToCheck, out primitiveTypeInfo) || !primitiveTypeInfo.IsIntegral)
                    throw new InvalidAttributeException("Properties decorated with BitPackerInteger or BitPackerLengthKey must be integral", this.debugName);

                this.bitWidth = integerAttribute.NullableBitWidth;
                if (this.bitWidth != null && !this.IsChildOfBitField)
                    throw new InvalidAttributeException("You may only set BitWidth on properties of a class decorated with BitPackerBitField", this.debugName);
                if (this.bitWidth != null && this.bitWidth.Value <= 0)
                    throw new InvalidAttributeException("Bit Width must be > 0", this.debugName);
            }

            // Has to happen after the IntegerAttribute stuff, as bitWidth must be set
            if (this.IsBoolean)
            {
                this.booleanEquivalentObjectDetails = new ObjectDetails(this.equivalentType, this, propertyAttributes.PopOrEmpty(), this.Endianness);
            }

            var arrayLengthAttribute = propertyAttribute as BitPackerLengthKeyAttribute;
            if (arrayLengthAttribute != null)
            {
                if (parent != null && parent.IsEnumerable)
                    throw new InvalidAttributeException("Length keys may not appear as array elements", this.debugName);

                // Type-checking already done by BitPackerIntegerAttribute
                this.lengthKey = arrayLengthAttribute.LengthKey;
            }

            // Has to be done after we've got the equivalent type
            if (this.IsChildOfBitField && !(PrimitiveTypes.Types.TryGetValue(this.equivalentType ?? this.type, out primitiveTypeInfo) && primitiveTypeInfo.IsIntegral))
                throw new InvalidAttributeException("Objects decorated with BitPackerBitField may only container integer fields", this.debugName);
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
            if (this.objectAttribute != null || this.bitFieldAttribute != null)
            {
                var allProperties = (from property in this.type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                     let attributes = property.GetCustomAttributes<BitPackerMemberAttribute>(false).OrderByDescending(x => x.Order)
                                     let firstAttribute = attributes.FirstOrDefault()
                                     where firstAttribute != null
                                     let attributesStack = ImmutableStack.From(attributes) // Put the one with the lowest order at the top of the stack
                                     orderby firstAttribute.Order
                                     select new PropertyObjectDetails(this.type, this, property, attributesStack, this.Endianness)).ToList();

                var properties = allProperties.ToList();
                foreach (var property in properties)
                {
                    property.Discover();
                }

                if (this.IsBitField)
                {
                    var bitFieldWidthBits = properties.Sum(x => x.BitWidth.Value);
                    if (bitFieldWidthBits > this.BitfieldWidthBytes * 8)
                        throw new InvalidAttributeException(String.Format("Object has width {0} bits, but its fields sum to {1} bits", this.BitfieldWidthBytes * 8, bitFieldWidthBits), this.debugName);
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

        public PropertyObjectDetails(Type parentType, ObjectDetails parent, PropertyInfo propertyInfo, ImmutableStack<BitPackerMemberAttribute> propertyAttributes, Endianness? endianness = null)
            : base(propertyInfo.PropertyType, String.Format("{0}.{1}", parentType.Description(), propertyInfo.Name), parent, propertyAttributes, endianness)
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
        public EnumerableElementObjectDetails(Type type, ObjectDetails parent, ImmutableStack<BitPackerMemberAttribute> propertyAttributes, Endianness? endianness = null)
            : base(type, parent, propertyAttributes, endianness)
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
