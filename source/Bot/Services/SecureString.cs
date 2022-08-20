using System;
using System.Security.Cryptography;
using System.Threading;

namespace Bot.Services
{

    /// <summary>
    ///     A light-weight string generation class.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public static class SecureString
    {
        private const int PoolSize = 4096;
        private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ‏";
        private static readonly ThreadLocal<byte[]> BytePoolProvider = new(() =>
        {
            return RandomNumberGenerator.GetBytes(PoolSize);
        });
        private static readonly ThreadLocal<int> CurrentIndex = new(() => -1); // This is incremented before use.



        /// <summary>Generates a string of an arbitrary length of random content.</summary>
        /// <param name="size">The length of string to be generated</param>
        /// <returns><see cref="string" /> that is of length <paramref name="size" /></returns>
        /// <exception cref="CryptographicException">The cryptographic service provider (CSP) cannot be acquired. </exception>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        /// <exception cref="MemberAccessException">The <see cref="T:System.Lazy`1" /> instance is initialized to use the default constructor of the type that is being lazily initialized, and permissions to access the constructor are missing.</exception>
        /// <exception cref="MissingMemberException">The <see cref="T:System.Lazy`1" /> instance is initialized to use the default constructor of the type that is being lazily initialized, and that type does not have a public, parameterless constructor.</exception>
        /// <exception cref="InvalidOperationException">The initialization function tries to access <see cref="Lazy{T}.Value" /> on this instance.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="System.Threading.ThreadLocal{T}" /> instance has been disposed.</exception>
        /// <threadsafety static="true" instance="false"/>
        public static unsafe string Create(int size)
        {
            /*
                This method requires a bit of explanation as it's doing some relatively obtuse actions in .NET.
                Effectively, the goal of this routine is to generate arbitrarily sized random strings as fast as it possibly can. These strings
                are used everywhere in our system. Sometimes they make their way into the database to mark rows, other times they are used for
                in-memory operations to uniquely identify an item in a collection. Regardless, it does not matter. These are unique enough
                values (granted, the chance of collision is 1 in several billions).

                Here's a high level explanation of the WHY on various points of the code.
                1) We use a BytePoolProvider (fancy variable declaration for a byte[] filled from a RandomNumberGenerator) that is ThreadLocal. This is to give us thread safety.
                2) We have to keep track of what we've consumed on the byte[] without actually causing additional allocations except for when we exhaust the byte[]
                    This is achieved with another ThreadLocal int that is the current index on the BytePoolProvider.
                3) We allocate a NULL string of the appropriate length up front. This is an optimization as we later pin that string as a character array and manipulate the character array.
                    This is quite odd to do, but, it's done for performance reasons. Assigning to the character array is faster than trying to build a string in memory. And since
                    we already paid to create the string up front, we'll just mess with the underlying array.
                4) You might notice we throw away bytes greater than 252. This is to prevent skewing. Base36, our array of characters to map to, is 252 characters long. It's the highest byte value
                    before the Base36 cycle repeats. For the curious, Base36 is as follows: 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ. This is 36 characters long. 36 * 7 = 252, the closest multiple of 36 to 255 (max value of a byte!).
					If we were to make Base36.Length equal to 255, we would be skewing results ever so slightly (as we would have extra possible occurrences for 0, 1, and 2). So, instead, we disregard byte values that are outside of our
                    range. This means we do not, theoretically, show any additional favor to other characters.
                5) After we selected the right character, we get a little cheeky. We copy a char from Base36's character array to the NULL array we created earlier. The appropriate Base36 character index is determined
                    from the byte we pulled from the byte-pool and we keep track of the appropriate index to copy into our return value.

                All said and done, this method plays a little fast and loose with pointers to achieve the speed we want. But it's mostly safe and handles even the largest of strings you could possibly throw at it.
            */

            // Grab our ThreadLocal values for the byte pool and current index into that pool.
            var index = CurrentIndex.Value;

            // Allocate the NULL string here of the correct size.
            var newString = new string('\0', size);

            // Get a pointer to the first character of newString and Base36.
            fixed (char* buffer = newString, b36 = Base36)
            {
                // Get a pointer to the byte pool
                fixed (byte* pbyte = BytePoolProvider.Value)
                {
                    for (var cnt = 0; cnt < size; cnt++)
                    {
                        // Make sure our pool is still good. If it's not, trash it and refill it.
                        if (++index >= PoolSize)
                        {
                            BytePoolProvider.Value = RandomNumberGenerator.GetBytes(PoolSize);
                            index = 0;
                        }
                        byte currentValue;

                        // Skip bytes with value 252 or greater, as they will skew the results.
                        // Remember, indexes are zero based! So we want 0 <= value <= 251!
                        while ((currentValue = pbyte[index]) >= 252)
                        {
                            //Bounds checking to make sure we don't go past the PoolSize
                            if (++index < PoolSize) continue;
                            BytePoolProvider.Value = RandomNumberGenerator.GetBytes(PoolSize);
                            index = 0;
                        }
                        // Copy the value over.
                        buffer[cnt] = b36[currentValue];
                    }
                    //Store the index value back to the thread value.
                    CurrentIndex.Value = index;
                    //After we've done all that, we want the string, right?
                    return newString;
                }
            }
        }

    }
}
