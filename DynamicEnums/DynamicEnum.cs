using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace DynamicEnums {
    /// <summary>
    /// A dynamic enum is a class that represents enum-like single-instance value behavior with additional capabilities, including dynamic addition of new arbitrary values.
    /// A dynamic enum uses <see cref="BigInteger"/> as its underlying type, allowing for an arbitrary number of enum values to be created, even when a <see cref="FlagsAttribute"/>-like structure is used that would only allow for up to 64 values in a regular enum.
    /// All boolean operations including <see cref="And{T}"/>, <see cref="Or{T}"/>, <see cref="Xor{T}"/> and <see cref="Neg{T}"/> are supported and can be implemented in derived classes using operator overloads.
    /// To create a custom dynamic enum, simply create a class that extends <see cref="DynamicEnum"/>. New values can then be added using <see cref="Add{T}"/>, <see cref="AddValue{T}"/> or <see cref="AddFlag{T}"/>.
    /// </summary>
    /// <remarks>
    /// To include enum-like operator overloads in a dynamic enum named MyEnum, the following code can be used:
    /// <code>
    /// public static implicit operator BigInteger(MyEnum value) => DynamicEnum.GetValue(value);
    /// public static implicit operator MyEnum(BigInteger value) => DynamicEnum.GetEnumValue&lt;MyEnum&gt;(value);
    /// public static MyEnum operator |(MyEnum left, MyEnum right) => DynamicEnum.Or(left, right);
    /// public static MyEnum operator &amp;(MyEnum left, MyEnum right) => DynamicEnum.And(left, right);
    /// public static MyEnum operator ^(MyEnum left, MyEnum right) => DynamicEnum.Xor(left, right);
    /// public static MyEnum operator ~(MyEnum value) => DynamicEnum.Neg(value);
    /// </code>
    /// </remarks>
    public abstract class DynamicEnum {

        private const BindingFlags ConstructorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type[] ConstructorTypes = {typeof(string), typeof(BigInteger), typeof(bool)};
        private static readonly Dictionary<Type, Storage> Storages = new Dictionary<Type, Storage>();

        private Dictionary<DynamicEnum, bool> allFlagsCache;
        private Dictionary<DynamicEnum, bool> anyFlagsCache;
        private BigInteger value;
        private string name;

        /// <summary>
        /// Creates a new dynamic enum instance.
        /// This constructor is protected as it is only invoked via reflection.
        /// This constructor is only called if the class doesn't have the <see cref="DynamicEnum(string,BigInteger,bool)"/> constructor.
        /// </summary>
        protected DynamicEnum() {}

        /// <summary>
        /// Creates a new dynamic enum instance.
        /// This constructor is protected as it is only invoked via reflection.
        /// </summary>
        /// <param name="name">The name of the enum value</param>
        /// <param name="value">The value</param>
        /// <param name="defined">Whether this enum value <see cref="IsDefined(DynamicEnum)"/>, and thus, not a combined flag.</param>
        protected DynamicEnum(string name, BigInteger value, bool defined) {
            this.name = name;
            this.value = value;
        }

        /// <summary>
        /// Returns true if this enum value has all of the given <see cref="DynamicEnum"/> flags on it.
        /// This operation is equivalent to <see cref="Enum.HasFlag"/>.
        /// </summary>
        /// <seealso cref="HasAnyFlags"/>
        /// <param name="flags">The flags to query</param>
        /// <returns>True if all of the flags are present, false otherwise</returns>
        public bool HasAllFlags(DynamicEnum flags) {
            if (this.allFlagsCache == null)
                this.allFlagsCache = new Dictionary<DynamicEnum, bool>();
            if (!this.allFlagsCache.TryGetValue(flags, out var ret)) {
                ret = (DynamicEnum.GetValue(this) & DynamicEnum.GetValue(flags)) == DynamicEnum.GetValue(flags);
                this.allFlagsCache.Add(flags, ret);
            }
            return ret;
        }

        /// <summary>
        /// Returns true if this enum value has any of the given <see cref="DynamicEnum"/> flags on it
        /// </summary>
        /// <seealso cref="HasAllFlags"/>
        /// <param name="flags">The flags to query</param>
        /// <returns>True if one of the flags is present, false otherwise</returns>
        public bool HasAnyFlags(DynamicEnum flags) {
            if (this.anyFlagsCache == null)
                this.anyFlagsCache = new Dictionary<DynamicEnum, bool>();
            if (!this.anyFlagsCache.TryGetValue(flags, out var ret)) {
                ret = (DynamicEnum.GetValue(this) & DynamicEnum.GetValue(flags)) != 0;
                this.anyFlagsCache.Add(flags, ret);
            }
            return ret;
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() {
            if (this.name != null)
                return this.name;

            var storage = DynamicEnum.GetStorage(this.GetType());
            if (!storage.CombinedNameCache.TryGetValue(this, out var combinedName)) {
                // Enum ToString remarks: https://learn.microsoft.com/en-us/dotnet/api/system.enum.tostring
                // If the FlagsAttribute is not applied to this enumerated type and there is a named constant equal to the value of this instance, then the return value is a string containing the name of the constant.
                // If the FlagsAttribute is applied and there is a combination of one or more named constants equal to the value of this instance, then the return value is a string containing a delimiter-separated list of the names of the constants.
                // Otherwise, the return value is the string representation of the numeric value of this instance.
                var included = new StringBuilder();
                var remain = DynamicEnum.GetValue(this);
                foreach (var other in storage.Values.Values) {
                    if (this.HasAllFlags(other)) {
                        var otherValue = DynamicEnum.GetValue(other);
                        if (otherValue != 0) {
                            if (included.Length > 0)
                                included.Append(", ");
                            included.Append(other);
                            remain &= ~otherValue;
                        }
                    }
                }
                combinedName = included.Length > 0 && remain == 0 ? included.ToString() : DynamicEnum.GetValue(this).ToString();
                storage.CombinedNameCache.Add(this, combinedName);
            }
            return combinedName;
        }

        /// <summary>
        /// Adds a new enum value to the given enum type <typeparamref name="T"/>
        /// </summary>
        /// <param name="name">The name of the enum value to add</param>
        /// <param name="value">The value to add</param>
        /// <typeparam name="T">The type to add this value to</typeparam>
        /// <returns>The newly created enum value</returns>
        /// <exception cref="ArgumentException">Thrown if the name or value passed are already present</exception>
        public static T Add<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(string name, BigInteger value) where T : DynamicEnum {
            var storage = DynamicEnum.GetStorage(typeof(T));

            // cached parsed values and names might be incomplete with new values
            storage.ClearCaches();

            if (storage.Values.ContainsKey(value))
                throw new ArgumentException($"Duplicate value {value}", nameof(value));
            foreach (var v in storage.Values.Values) {
                if (v.name == name)
                    throw new ArgumentException($"Duplicate name {name}", nameof(name));
            }

            var ret = DynamicEnum.Construct(typeof(T), name, value, true);
            storage.Values.Add(value, ret);
            return (T) ret;
        }

        /// <summary>
        /// Adds a new enum value to the given enum type <typeparamref name="T"/>.
        /// This method differs from <see cref="Add{T}"/> in that it automatically determines a value.
        /// The value determined will be the next free number in a sequence, which represents the default behavior in an enum if enum values are not explicitly numbered.
        /// </summary>
        /// <param name="name">The name of the enum value to add</param>
        /// <typeparam name="T">The type to add this value to</typeparam>
        /// <returns>The newly created enum value</returns>
        public static T AddValue<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(string name) where T : DynamicEnum {
            BigInteger value = 0;
            while (DynamicEnum.IsDefined(typeof(T), value))
                value++;
            return DynamicEnum.Add<T>(name, value);
        }

        /// <summary>
        /// Adds a new flag enum value to the given enum type <typeparamref name="T"/>.
        /// This method differs from <see cref="Add{T}"/> in that it automatically determines a value.
        /// The value determined will be the next free power of two, allowing enum values to be combined using bitwise operations to create <see cref="FlagsAttribute"/>-like behavior.
        /// </summary>
        /// <param name="name">The name of the enum value to add</param>
        /// <typeparam name="T">The type to add this value to</typeparam>
        /// <returns>The newly created enum value</returns>
        public static T AddFlag<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(string name) where T : DynamicEnum {
            BigInteger value = 1;
            while (DynamicEnum.IsDefined(typeof(T), value))
                value <<= 1;
            return DynamicEnum.Add<T>(name, value);
        }

        /// <summary>
        /// Returns a collection of all of the enum values that are explicitly defined for the given dynamic enum type <typeparamref name="T"/>.
        /// A value counts as explicitly defined if it has been added using <see cref="Add{T}"/>, <see cref="AddValue{T}"/> or <see cref="AddFlag{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type whose values to get</typeparam>
        /// <returns>The defined values for the given type, in the order they were added.</returns>
        public static IEnumerable<T> GetValues<T>() where T : DynamicEnum {
            return DynamicEnum.GetValues(typeof(T)).Cast<T>();
        }

        /// <summary>
        /// Returns a collection of all of the enum values that are explicitly defined for the given dynamic enum type <typeparamref name="T"/>, excluding any explicitly defined combined flags.
        /// A value counts as explicitly defined if it has been added using <see cref="Add{T}"/>, <see cref="AddValue{T}"/> or <see cref="AddFlag{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type whose values to get.</typeparam>
        /// <returns>The defined values for the given type, in ascending value order, excluding combined flags.</returns>
        public static IEnumerable<T> GetUniqueValues<T>() where T : DynamicEnum {
            var used = BigInteger.Zero;
            foreach (var value in DynamicEnum.GetValues<T>().OrderBy(DynamicEnum.GetValue)) {
                var iValue = DynamicEnum.GetValue(value);
                if ((used & iValue) == 0) {
                    yield return value;
                    used |= iValue;
                }
            }
        }

        /// <summary>
        /// Returns a collection of all of the enum values that are explicitly defined for the given dynamic enum type <paramref name="type"/>.
        /// A value counts as explicitly defined if it has been added using <see cref="Add{T}"/>, <see cref="AddValue{T}"/> or <see cref="AddFlag{T}"/>.
        /// </summary>
        /// <param name="type">The type whose values to get</param>
        /// <returns>The defined values for the given type, in the order they were added.</returns>
        public static IEnumerable<DynamicEnum> GetValues(Type type) {
            return DynamicEnum.GetStorage(type).Values.Values;
        }

        /// <summary>
        /// Returns all of the defined values from the given dynamic enum type <typeparamref name="T"/> which are contained in <paramref name="combinedFlag"/>.
        /// Note that, if combined flags are defined in <typeparamref name="T"/>, and <paramref name="combinedFlag"/> contains them, they will also be returned.
        /// </summary>
        /// <param name="combinedFlag">The combined flags whose individual flags to return.</param>
        /// <param name="includeZero">Whether the enum value 0 should also be returned, if <typeparamref name="T"/> contains one.</param>
        /// <typeparam name="T">The type of enum.</typeparam>
        /// <returns>All of the flags that make up <paramref name="combinedFlag"/>.</returns>
        public static IEnumerable<T> GetFlags<T>(T combinedFlag, bool includeZero = true) where T : DynamicEnum {
            foreach (var flag in DynamicEnum.GetValues<T>()) {
                if (combinedFlag.HasAllFlags(flag) && (includeZero || DynamicEnum.GetValue(flag) != BigInteger.Zero))
                    yield return flag;
            }
        }

        /// <summary>
        /// Returns all of the defined unique flags from the given dynamic enum type <typeparamref name="T"/> which are contained in <paramref name="combinedFlag"/>.
        /// Any combined flags (flags that aren't powers of two) which are defined in <typeparamref name="T"/> will not be returned.
        /// </summary>
        /// <param name="combinedFlag">The combined flags whose individual flags to return.</param>
        /// <typeparam name="T">The type of enum.</typeparam>
        /// <returns>All of the unique flags that make up <paramref name="combinedFlag"/>.</returns>
        public static IEnumerable<T> GetUniqueFlags<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(T combinedFlag) where T : DynamicEnum {
            // we can't use the same method here as EnumHelper.GetUniqueFlags since DynamicEnum doesn't guarantee sorted values
            var max = DynamicEnum.GetValues<T>().Max(DynamicEnum.GetValue);
            var uniqueFlag = BigInteger.One;
            while (uniqueFlag <= max) {
                if (DynamicEnum.IsDefined(typeof(T), uniqueFlag)) {
                    var uniqueFlagValue = DynamicEnum.GetEnumValue<T>(uniqueFlag);
                    if (combinedFlag.HasAnyFlags(uniqueFlagValue))
                        yield return uniqueFlagValue;
                }
                uniqueFlag <<= 1;
            }
        }

        /// <summary>
        /// Returns the bitwise OR (|) combination of the two dynamic enum values
        /// </summary>
        /// <param name="left">The left value</param>
        /// <param name="right">The right value</param>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <returns>The bitwise OR (|) combination</returns>
        public static T Or<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(T left, T right) where T : DynamicEnum {
            var cache = DynamicEnum.GetStorage(typeof(T)).OrCache;
            if (!cache.TryGetValue((left, right), out var ret)) {
                ret = DynamicEnum.GetEnumValue<T>(DynamicEnum.GetValue(left) | DynamicEnum.GetValue(right));
                cache.Add((left, right), ret);
            }
            return (T) ret;
        }

        /// <summary>
        /// Returns the bitwise AND (&amp;) combination of the two dynamic enum values
        /// </summary>
        /// <param name="left">The left value</param>
        /// <param name="right">The right value</param>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <returns>The bitwise AND (&amp;) combination</returns>
        public static T And<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(T left, T right) where T : DynamicEnum {
            var cache = DynamicEnum.GetStorage(typeof(T)).AndCache;
            if (!cache.TryGetValue((left, right), out var ret)) {
                ret = DynamicEnum.GetEnumValue<T>(DynamicEnum.GetValue(left) & DynamicEnum.GetValue(right));
                cache.Add((left, right), ret);
            }
            return (T) ret;
        }

        /// <summary>
        /// Returns the bitwise XOR (^) combination of the two dynamic enum values
        /// </summary>
        /// <param name="left">The left value</param>
        /// <param name="right">The right value</param>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <returns>The bitwise XOR (^) combination</returns>
        public static T Xor<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(T left, T right) where T : DynamicEnum {
            var cache = DynamicEnum.GetStorage(typeof(T)).XorCache;
            if (!cache.TryGetValue((left, right), out var ret)) {
                ret = DynamicEnum.GetEnumValue<T>(DynamicEnum.GetValue(left) ^ DynamicEnum.GetValue(right));
                cache.Add((left, right), ret);
            }
            return (T) ret;
        }

        /// <summary>
        /// Returns the bitwise NEG (~) combination of the dynamic enum value
        /// </summary>
        /// <param name="value">The value</param>
        /// <typeparam name="T">The type of the values</typeparam>
        /// <returns>The bitwise NEG (~) value</returns>
        public static T Neg<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(T value) where T : DynamicEnum {
            var cache = DynamicEnum.GetStorage(typeof(T)).NegCache;
            if (!cache.TryGetValue(value, out var ret)) {
                ret = DynamicEnum.GetEnumValue<T>(~DynamicEnum.GetValue(value));
                cache.Add(value, ret);
            }
            return (T) ret;
        }

        /// <summary>
        /// Returns the <see cref="BigInteger"/> representation of the given dynamic enum value
        /// </summary>
        /// <param name="value">The value whose number representation to get</param>
        /// <returns>The value's number representation</returns>
        public static BigInteger GetValue(DynamicEnum value) {
            return value?.value ?? 0;
        }

        /// <summary>
        /// Returns the defined or combined dynamic enum value for the given <see cref="BigInteger"/> representation
        /// </summary>
        /// <param name="value">The value whose dynamic enum value to get</param>
        /// <typeparam name="T">The type that the returned dynamic enum should have</typeparam>
        /// <returns>The defined or combined dynamic enum value</returns>
        public static T GetEnumValue<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(BigInteger value) where T : DynamicEnum {
            return (T) DynamicEnum.GetEnumValue(typeof(T), value);
        }

        /// <summary>
        /// Returns the defined or combined dynamic enum value for the given <see cref="BigInteger"/> representation
        /// </summary>
        /// <param name="type">The type that the returned dynamic enum should have</param>
        /// <param name="value">The value whose dynamic enum value to get</param>
        /// <returns>The defined or combined dynamic enum value</returns>
        public static DynamicEnum GetEnumValue(
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            Type type, BigInteger value) {
            var storage = DynamicEnum.GetStorage(type);

            // get the defined value if it exists
            if (storage.Values.TryGetValue(value, out var defined))
                return defined;

            // otherwise, cache the combined value
            if (!storage.FlagCache.TryGetValue(value, out var combined)) {
                combined = DynamicEnum.Construct(type, null, value, false);
                storage.FlagCache.Add(value, combined);
            }
            return combined;
        }

        /// <summary>
        /// Parses the given <see cref="string"/> into a dynamic enum value and returns the result.
        /// This method supports defined enum values as well as values combined using the pipe (|) character and any number of spaces.
        /// If no enum value can be parsed, null is returned.
        /// </summary>
        /// <param name="strg">The string to parse into a dynamic enum value</param>
        /// <typeparam name="T">The type of the dynamic enum value to parse</typeparam>
        /// <returns>The parsed enum value, or null if parsing fails</returns>
        public static T Parse<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            T>(string strg) where T : DynamicEnum {
            return (T) DynamicEnum.Parse(typeof(T), strg);
        }

        /// <summary>
        /// Parses the given <see cref="string"/> into a dynamic enum value and returns the result.
        /// This method supports defined enum values as well as values combined using the pipe (|) character and any number of spaces.
        /// If no enum value can be parsed, null is returned.
        /// </summary>
        /// <param name="type">The type of the dynamic enum value to parse</param>
        /// <param name="strg">The string to parse into a dynamic enum value</param>
        /// <returns>The parsed enum value, or null if parsing fails</returns>
        public static DynamicEnum Parse(
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            Type type, string strg) {
            var cache = DynamicEnum.GetStorage(type).ParseCache;
            if (!cache.TryGetValue(strg, out var cached)) {
                BigInteger? accum = null;
                foreach (var val in strg.Split(',')) {
                    foreach (var defined in DynamicEnum.GetValues(type)) {
                        if (defined.name == val.Trim()) {
                            accum = (accum ?? 0) | DynamicEnum.GetValue(defined);
                            break;
                        }
                    }
                }
                if (accum != null)
                    cached = DynamicEnum.GetEnumValue(type, accum.Value);
                cache.Add(strg, cached);
            }
            return cached;
        }

        /// <summary>
        /// Returns whether the given <paramref name="value"/> is defined in the given dynamic enum <paramref name="type"/>.
        /// A value counts as explicitly defined if it has been added using <see cref="Add{T}"/>, <see cref="AddValue{T}"/> or <see cref="AddFlag{T}"/>.
        /// </summary>
        /// <param name="type">The dynamic enum type to query.</param>
        /// <param name="value">The value to query.</param>
        /// <returns>Whether the <paramref name="value"/> is defined.</returns>
        public static bool IsDefined(Type type, BigInteger value) {
            return DynamicEnum.GetStorage(type).Values.ContainsKey(value);
        }

        /// <summary>
        /// Returns whether the given <paramref name="value"/> is defined in its dynamic enum type.
        /// A value counts as explicitly defined if it has been added using <see cref="Add{T}"/>, <see cref="AddValue{T}"/> or <see cref="AddFlag{T}"/>.
        /// </summary>
        /// <param name="value">The value to query.</param>
        /// <returns>Whether the <paramref name="value"/> is defined.</returns>
        public static bool IsDefined(DynamicEnum value) {
            return value != null && DynamicEnum.IsDefined(value.GetType(), DynamicEnum.GetValue(value));
        }

        private static Storage GetStorage(Type type) {
            if (!DynamicEnum.Storages.TryGetValue(type, out var storage)) {
                storage = new Storage();
                DynamicEnum.Storages.Add(type, storage);
            }
            return storage;
        }

        private static DynamicEnum Construct(
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            Type type, string name, BigInteger value, bool defined) {
            // try to call the constructor with parameters first
            var parameterConstructor = type.GetConstructor(DynamicEnum.ConstructorFlags, null, DynamicEnum.ConstructorTypes, null);
            if (parameterConstructor != null)
                return (DynamicEnum) parameterConstructor.Invoke(DynamicEnum.ConstructorFlags, null, new object[] {name, value, defined}, CultureInfo.InvariantCulture);

            // default to the empty constructor and set the values manually
            var emptyConstructor = type.GetConstructor(DynamicEnum.ConstructorFlags, null, Type.EmptyTypes, null);
            var ret = (DynamicEnum) emptyConstructor.Invoke(DynamicEnum.ConstructorFlags, null, null, CultureInfo.InvariantCulture);
            ret.name = name;
            ret.value = value;
            return ret;
        }

        private class Storage {

            public readonly Dictionary<BigInteger, DynamicEnum> Values = new Dictionary<BigInteger, DynamicEnum>();
            public readonly Dictionary<BigInteger, DynamicEnum> FlagCache = new Dictionary<BigInteger, DynamicEnum>();
            public readonly Dictionary<string, DynamicEnum> ParseCache = new Dictionary<string, DynamicEnum>();
            public readonly Dictionary<(DynamicEnum, DynamicEnum), DynamicEnum> OrCache = new Dictionary<(DynamicEnum, DynamicEnum), DynamicEnum>();
            public readonly Dictionary<(DynamicEnum, DynamicEnum), DynamicEnum> AndCache = new Dictionary<(DynamicEnum, DynamicEnum), DynamicEnum>();
            public readonly Dictionary<(DynamicEnum, DynamicEnum), DynamicEnum> XorCache = new Dictionary<(DynamicEnum, DynamicEnum), DynamicEnum>();
            public readonly Dictionary<DynamicEnum, DynamicEnum> NegCache = new Dictionary<DynamicEnum, DynamicEnum>();
            public readonly Dictionary<DynamicEnum, string> CombinedNameCache = new Dictionary<DynamicEnum, string>();

            public void ClearCaches() {
                this.FlagCache.Clear();
                this.ParseCache.Clear();
                this.OrCache.Clear();
                this.AndCache.Clear();
                this.XorCache.Clear();
                this.NegCache.Clear();
                this.CombinedNameCache.Clear();
            }

        }

    }
}
