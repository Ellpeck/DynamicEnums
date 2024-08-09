using System;
using System.Collections.Generic;

namespace DynamicEnums {
    /// <summary>
    /// A helper class that allows easier usage of regular <see cref="Enum"/> values, as well as <see cref="FlagsAttribute"/> <see cref="Enum"/> values.
    /// </summary>
    public static class EnumHelper {

        /// <summary>
        /// Returns an array containing all of the values of the given enum type.
        /// Note that this method is a version-independent equivalent of .NET 5's <c>Enum.GetValues&lt;TEnum&gt;</c>.
        /// </summary>
        /// <typeparam name="T">The type whose enum to get</typeparam>
        /// <returns>An enumerable of the values of the enum, in ascending value order.</returns>
        public static T[] GetValues<T>() where T : struct, Enum {
#if NET6_0_OR_GREATER
            return Enum.GetValues<T>();
#else
            return (T[]) Enum.GetValues(typeof(T));
#endif
        }

        /// <summary>
        /// Returns an array containing all of the values of the given enum type, excluding any explicitly defined combined flags.
        /// </summary>
        /// <typeparam name="T">The type whose enum to get.</typeparam>
        /// <returns>An enumerable of the values of the enum, in ascending value order, excluding combined flags.</returns>
        public static IEnumerable<T> GetUniqueValues<T>() where T : struct, Enum {
            var used = 0L;
            foreach (var value in EnumHelper.GetValues<T>()) {
                var lValue = Convert.ToInt64(value);
                if ((used & lValue) == 0) {
                    yield return value;
                    used |= lValue;
                }
            }
        }

        /// <summary>
        /// Returns true if the given <paramref name="value"/> has all of the given <see cref="Enum"/> flags on it.
        /// This operation is equivalent to <see cref="Enum.HasFlag"/>, but doesn't do any additional checks, making it faster.
        /// </summary>
        /// <seealso cref="HasAnyFlags{T}"/>
        /// <param name="value">The value to query flags on.</param>
        /// <param name="flags">The flags to query.</param>
        /// <returns>True if all of the flags are present, false otherwise.</returns>
        public static bool HasAllFlags<T>(this T value, T flags) where T : struct, Enum {
            return (Convert.ToInt64(value) & Convert.ToInt64(flags)) == Convert.ToInt64(flags);
        }

        /// <summary>
        /// Returns true if the given <paramref name="value"/> has any of the given <see cref="Enum"/> flags on it.
        /// </summary>
        /// <seealso cref="HasAllFlags{T}"/>
        /// <param name="value">The value to query flags on.</param>
        /// <param name="flags">The flags to query.</param>
        /// <returns>True if any of the flags are present, false otherwise.</returns>
        public static bool HasAnyFlags<T>(this T value, T flags) where T : struct, Enum {
            return (Convert.ToInt64(value) & Convert.ToInt64(flags)) != 0;
        }

        /// <summary>
        /// Returns all of the defined values from the given enum type <typeparamref name="T"/> which are contained in <paramref name="combinedFlag"/>.
        /// Note that, if combined flags are defined in <typeparamref name="T"/>, and <paramref name="combinedFlag"/> contains them, they will also be returned.
        /// </summary>
        /// <param name="combinedFlag">The combined flags whose individual flags to return.</param>
        /// <param name="includeZero">Whether the enum value 0 should also be returned, if <typeparamref name="T"/> contains one.</param>
        /// <typeparam name="T">The type of enum.</typeparam>
        /// <returns>All of the flags that make up <paramref name="combinedFlag"/>.</returns>
        public static IEnumerable<T> GetFlags<T>(T combinedFlag, bool includeZero = true) where T : struct, Enum {
            foreach (var flag in EnumHelper.GetValues<T>()) {
                if (combinedFlag.HasAllFlags(flag) && (includeZero || Convert.ToInt64(flag) != 0))
                    yield return flag;
            }
        }

        /// <summary>
        /// Returns all of the defined unique flags from the given enum type <typeparamref name="T"/> which are contained in <paramref name="combinedFlag"/>.
        /// Any combined flags (flags that aren't powers of two) which are defined in <typeparamref name="T"/> will not be returned.
        /// </summary>
        /// <param name="combinedFlag">The combined flags whose individual flags to return.</param>
        /// <typeparam name="T">The type of enum.</typeparam>
        /// <returns>All of the unique flags that make up <paramref name="combinedFlag"/>.</returns>
        public static IEnumerable<T> GetUniqueFlags<T>(T combinedFlag) where T : struct, Enum {
            var uniqueFlag = 1L;
            foreach (var flag in EnumHelper.GetValues<T>()) {
                var flagValue = Convert.ToInt64(flag);
                // GetValues is always ordered by binary value, so we can be sure that the next flag is bigger than the last
                while (uniqueFlag < flagValue)
                    uniqueFlag <<= 1;
                if (flagValue == uniqueFlag && combinedFlag.HasAllFlags(flag))
                    yield return flag;
            }
        }

    }
}
