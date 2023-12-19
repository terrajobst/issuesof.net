// Generated by Snowball 2.2.0 - https://snowballstem.org/

#pragma warning disable 0164
#pragma warning disable 0162

namespace Snowball
{
    using System;
    using System.Text;
    
    ///<summary>
    ///  This class implements the stemming algorithm defined by a snowball script.
    ///  Generated by Snowball 2.2.0 - https://snowballstem.org/
    ///</summary>
    /// 
    [System.CodeDom.Compiler.GeneratedCode("Snowball", "2.2.0")]
    public partial class FinnishStemmer : Stemmer
    {
        private bool B_ending_removed;
        private StringBuilder S_x = new StringBuilder();
        private int I_p2;
        private int I_p1;

        private const string g_AEI = "a\u00E4ei";
        private const string g_C = "bcdfghjklmnpqrstvwxz";
        private const string g_V1 = "aeiouy\u00E4\u00F6";
        private const string g_V2 = "aeiou\u00E4\u00F6";
        private const string g_particle_end = "aeiouy\u00E4\u00F6nt";
        private static readonly Among[] a_0 = new[] 
        {
            new Among("pa", -1, 1),
            new Among("sti", -1, 2),
            new Among("kaan", -1, 1),
            new Among("han", -1, 1),
            new Among("kin", -1, 1),
            new Among("h\u00E4n", -1, 1),
            new Among("k\u00E4\u00E4n", -1, 1),
            new Among("ko", -1, 1),
            new Among("p\u00E4", -1, 1),
            new Among("k\u00F6", -1, 1)
        };

        private static readonly Among[] a_1 = new[] 
        {
            new Among("lla", -1, -1),
            new Among("na", -1, -1),
            new Among("ssa", -1, -1),
            new Among("ta", -1, -1),
            new Among("lta", 3, -1),
            new Among("sta", 3, -1)
        };

        private static readonly Among[] a_2 = new[] 
        {
            new Among("ll\u00E4", -1, -1),
            new Among("n\u00E4", -1, -1),
            new Among("ss\u00E4", -1, -1),
            new Among("t\u00E4", -1, -1),
            new Among("lt\u00E4", 3, -1),
            new Among("st\u00E4", 3, -1)
        };

        private static readonly Among[] a_3 = new[] 
        {
            new Among("lle", -1, -1),
            new Among("ine", -1, -1)
        };

        private static readonly Among[] a_4 = new[] 
        {
            new Among("nsa", -1, 3),
            new Among("mme", -1, 3),
            new Among("nne", -1, 3),
            new Among("ni", -1, 2),
            new Among("si", -1, 1),
            new Among("an", -1, 4),
            new Among("en", -1, 6),
            new Among("\u00E4n", -1, 5),
            new Among("ns\u00E4", -1, 3)
        };

        private static readonly Among[] a_5 = new[] 
        {
            new Among("aa", -1, -1),
            new Among("ee", -1, -1),
            new Among("ii", -1, -1),
            new Among("oo", -1, -1),
            new Among("uu", -1, -1),
            new Among("\u00E4\u00E4", -1, -1),
            new Among("\u00F6\u00F6", -1, -1)
        };

        private readonly Among[] a_6;
        private static readonly Among[] a_7 = new[] 
        {
            new Among("eja", -1, -1),
            new Among("mma", -1, 1),
            new Among("imma", 1, -1),
            new Among("mpa", -1, 1),
            new Among("impa", 3, -1),
            new Among("mmi", -1, 1),
            new Among("immi", 5, -1),
            new Among("mpi", -1, 1),
            new Among("impi", 7, -1),
            new Among("ej\u00E4", -1, -1),
            new Among("mm\u00E4", -1, 1),
            new Among("imm\u00E4", 10, -1),
            new Among("mp\u00E4", -1, 1),
            new Among("imp\u00E4", 12, -1)
        };

        private static readonly Among[] a_8 = new[] 
        {
            new Among("i", -1, -1),
            new Among("j", -1, -1)
        };

        private static readonly Among[] a_9 = new[] 
        {
            new Among("mma", -1, 1),
            new Among("imma", 0, -1)
        };


        /// <summary>
        ///   Initializes a new instance of the <see cref="FinnishStemmer"/> class.
        /// </summary>
        /// 
        public FinnishStemmer()
        {
            a_6 = new[] 
            {
                new Among("a", -1, 8),
                new Among("lla", 0, -1),
                new Among("na", 0, -1),
                new Among("ssa", 0, -1),
                new Among("ta", 0, -1),
                new Among("lta", 4, -1),
                new Among("sta", 4, -1),
                new Among("tta", 4, 2),
                new Among("lle", -1, -1),
                new Among("ine", -1, -1),
                new Among("ksi", -1, -1),
                new Among("n", -1, 7),
                new Among("han", 11, 1),
                new Among("den", 11, -1, r_VI),
                new Among("seen", 11, -1, r_LONG),
                new Among("hen", 11, 2),
                new Among("tten", 11, -1, r_VI),
                new Among("hin", 11, 3),
                new Among("siin", 11, -1, r_VI),
                new Among("hon", 11, 4),
                new Among("h\u00E4n", 11, 5),
                new Among("h\u00F6n", 11, 6),
                new Among("\u00E4", -1, 8),
                new Among("ll\u00E4", 22, -1),
                new Among("n\u00E4", 22, -1),
                new Among("ss\u00E4", 22, -1),
                new Among("t\u00E4", 22, -1),
                new Among("lt\u00E4", 26, -1),
                new Among("st\u00E4", 26, -1),
                new Among("tt\u00E4", 26, 2)
            };

        }



        private bool r_mark_regions()
        {
            I_p1 = limit;
            I_p2 = limit;
            if (out_grouping(g_V1, 97, 246, true) < 0)
            {
                return false;
            }

            {

                int ret = in_grouping(g_V1, 97, 246, true);
                if (ret < 0)
                {
                    return false;
                }

                cursor += ret;
            }
            I_p1 = cursor;
            if (out_grouping(g_V1, 97, 246, true) < 0)
            {
                return false;
            }

            {

                int ret = in_grouping(g_V1, 97, 246, true);
                if (ret < 0)
                {
                    return false;
                }

                cursor += ret;
            }
            I_p2 = cursor;
            return true;
        }

        private bool r_R2()
        {
            if (!(I_p2 <= cursor))
            {
                return false;
            }
            return true;
        }

        private bool r_particle_etc()
        {
            int among_var;
            if (cursor < I_p1)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p1;
            ket = cursor;
            among_var = find_among_b(a_0);
            if (among_var == 0)
            {
                {
                    limit_backward = c2;
                    return false;
                }
            }
            bra = cursor;
            limit_backward = c2;
            switch (among_var) {
                case 1:
                    if (in_grouping_b(g_particle_end, 97, 246, false) != 0)
                    {
                        return false;
                    }
                    break;
                case 2:
                    if (!r_R2())
                        return false;
                    break;
            }
            slice_del();
            return true;
        }

        private bool r_possessive()
        {
            int among_var;
            if (cursor < I_p1)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p1;
            ket = cursor;
            among_var = find_among_b(a_4);
            if (among_var == 0)
            {
                {
                    limit_backward = c2;
                    return false;
                }
            }
            bra = cursor;
            limit_backward = c2;
            switch (among_var) {
                case 1:
                    {
                        int c3 = limit - cursor;
                        if (!(eq_s_b("k")))
                        {
                            goto lab0;
                        }
                        return false;
                    lab0: ; 
                        cursor = limit - c3;
                    }
                    slice_del();
                    break;
                case 2:
                    slice_del();
                    ket = cursor;
                    if (!(eq_s_b("kse")))
                    {
                        return false;
                    }
                    bra = cursor;
                    slice_from("ksi");
                    break;
                case 3:
                    slice_del();
                    break;
                case 4:
                    if (find_among_b(a_1) == 0)
                    {
                        return false;
                    }
                    slice_del();
                    break;
                case 5:
                    if (find_among_b(a_2) == 0)
                    {
                        return false;
                    }
                    slice_del();
                    break;
                case 6:
                    if (find_among_b(a_3) == 0)
                    {
                        return false;
                    }
                    slice_del();
                    break;
            }
            return true;
        }

        private bool r_LONG()
        {
            if (find_among_b(a_5) == 0)
            {
                return false;
            }
            return true;
        }

        private bool r_VI()
        {
            if (!(eq_s_b("i")))
            {
                return false;
            }
            if (in_grouping_b(g_V2, 97, 246, false) != 0)
            {
                return false;
            }
            return true;
        }

        private bool r_case_ending()
        {
            int among_var;
            if (cursor < I_p1)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p1;
            ket = cursor;
            among_var = find_among_b(a_6);
            if (among_var == 0)
            {
                {
                    limit_backward = c2;
                    return false;
                }
            }
            bra = cursor;
            limit_backward = c2;
            switch (among_var) {
                case 1:
                    if (!(eq_s_b("a")))
                    {
                        return false;
                    }
                    break;
                case 2:
                    if (!(eq_s_b("e")))
                    {
                        return false;
                    }
                    break;
                case 3:
                    if (!(eq_s_b("i")))
                    {
                        return false;
                    }
                    break;
                case 4:
                    if (!(eq_s_b("o")))
                    {
                        return false;
                    }
                    break;
                case 5:
                    if (!(eq_s_b("\u00E4")))
                    {
                        return false;
                    }
                    break;
                case 6:
                    if (!(eq_s_b("\u00F6")))
                    {
                        return false;
                    }
                    break;
                case 7:
                    {
                        int c3 = limit - cursor;
                        int c4 = limit - cursor;
                        {
                            int c5 = limit - cursor;
                            if (!r_LONG())
                                goto lab2;
                            goto lab1;
                        lab2: ; 
                            cursor = limit - c5;
                            if (!(eq_s_b("ie")))
                            {
                                {
                                    cursor = limit - c3;
                                    goto lab0;
                                }
                            }
                        }
                    lab1: ; 
                        cursor = limit - c4;
                        if (cursor <= limit_backward)
                        {
                            {
                                cursor = limit - c3;
                                goto lab0;
                            }
                        }
                        cursor--;
                        bra = cursor;
                    lab0: ; 
                    }
                    break;
                case 8:
                    if (in_grouping_b(g_V1, 97, 246, false) != 0)
                    {
                        return false;
                    }
                    if (in_grouping_b(g_C, 98, 122, false) != 0)
                    {
                        return false;
                    }
                    break;
            }
            slice_del();
            B_ending_removed = true;
            return true;
        }

        private bool r_other_endings()
        {
            int among_var;
            if (cursor < I_p2)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p2;
            ket = cursor;
            among_var = find_among_b(a_7);
            if (among_var == 0)
            {
                {
                    limit_backward = c2;
                    return false;
                }
            }
            bra = cursor;
            limit_backward = c2;
            switch (among_var) {
                case 1:
                    {
                        int c3 = limit - cursor;
                        if (!(eq_s_b("po")))
                        {
                            goto lab0;
                        }
                        return false;
                    lab0: ; 
                        cursor = limit - c3;
                    }
                    break;
            }
            slice_del();
            return true;
        }

        private bool r_i_plural()
        {
            if (cursor < I_p1)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p1;
            ket = cursor;
            if (find_among_b(a_8) == 0)
            {
                {
                    limit_backward = c2;
                    return false;
                }
            }
            bra = cursor;
            limit_backward = c2;
            slice_del();
            return true;
        }

        private bool r_t_plural()
        {
            int among_var;
            if (cursor < I_p1)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p1;
            ket = cursor;
            if (!(eq_s_b("t")))
            {
                {
                    limit_backward = c2;
                    return false;
                }
            }
            bra = cursor;
            {
                int c3 = limit - cursor;
                if (in_grouping_b(g_V1, 97, 246, false) != 0)
                {
                    {
                        limit_backward = c2;
                        return false;
                    }
                }
                cursor = limit - c3;
            }
            slice_del();
            limit_backward = c2;
            if (cursor < I_p2)
            {
                return false;
            }
            int c5 = limit_backward;
            limit_backward = I_p2;
            ket = cursor;
            among_var = find_among_b(a_9);
            if (among_var == 0)
            {
                {
                    limit_backward = c5;
                    return false;
                }
            }
            bra = cursor;
            limit_backward = c5;
            switch (among_var) {
                case 1:
                    {
                        int c6 = limit - cursor;
                        if (!(eq_s_b("po")))
                        {
                            goto lab0;
                        }
                        return false;
                    lab0: ; 
                        cursor = limit - c6;
                    }
                    break;
            }
            slice_del();
            return true;
        }

        private bool r_tidy()
        {
            if (cursor < I_p1)
            {
                return false;
            }
            int c2 = limit_backward;
            limit_backward = I_p1;
            {
                int c3 = limit - cursor;
                int c4 = limit - cursor;
                if (!r_LONG())
                    goto lab0;
                cursor = limit - c4;
                ket = cursor;
                if (cursor <= limit_backward)
                {
                    goto lab0;
                }
                cursor--;
                bra = cursor;
                slice_del();
            lab0: ; 
                cursor = limit - c3;
            }
            {
                int c5 = limit - cursor;
                ket = cursor;
                if (in_grouping_b(g_AEI, 97, 228, false) != 0)
                {
                    goto lab1;
                }
                bra = cursor;
                if (in_grouping_b(g_C, 98, 122, false) != 0)
                {
                    goto lab1;
                }
                slice_del();
            lab1: ; 
                cursor = limit - c5;
            }
            {
                int c6 = limit - cursor;
                ket = cursor;
                if (!(eq_s_b("j")))
                {
                    goto lab2;
                }
                bra = cursor;
                {
                    int c7 = limit - cursor;
                    if (!(eq_s_b("o")))
                    {
                        goto lab4;
                    }
                    goto lab3;
                lab4: ; 
                    cursor = limit - c7;
                    if (!(eq_s_b("u")))
                    {
                        goto lab2;
                    }
                }
            lab3: ; 
                slice_del();
            lab2: ; 
                cursor = limit - c6;
            }
            {
                int c8 = limit - cursor;
                ket = cursor;
                if (!(eq_s_b("o")))
                {
                    goto lab5;
                }
                bra = cursor;
                if (!(eq_s_b("j")))
                {
                    goto lab5;
                }
                slice_del();
            lab5: ; 
                cursor = limit - c8;
            }
            limit_backward = c2;
            if (in_grouping_b(g_V1, 97, 246, true) < 0)
            {
                return false;
            }

            ket = cursor;
            if (in_grouping_b(g_C, 98, 122, false) != 0)
            {
                return false;
            }
            bra = cursor;
            slice_to(S_x);
            if (!(eq_s_b(S_x)))
            {
                return false;
            }
            slice_del();
            return true;
        }

        protected override bool stem()
        {
            {
                int c1 = cursor;
                r_mark_regions();
                cursor = c1;
            }
            B_ending_removed = false;
            limit_backward = cursor;
            cursor = limit;
            {
                int c2 = limit - cursor;
                r_particle_etc();
                cursor = limit - c2;
            }
            {
                int c3 = limit - cursor;
                r_possessive();
                cursor = limit - c3;
            }
            {
                int c4 = limit - cursor;
                r_case_ending();
                cursor = limit - c4;
            }
            {
                int c5 = limit - cursor;
                r_other_endings();
                cursor = limit - c5;
            }
            if (!(B_ending_removed))
            {
                goto lab1;
            }
            {
                int c7 = limit - cursor;
                r_i_plural();
                cursor = limit - c7;
            }
            goto lab0;
        lab1: ; 
            {
                int c8 = limit - cursor;
                r_t_plural();
                cursor = limit - c8;
            }
        lab0: ; 
            {
                int c9 = limit - cursor;
                r_tidy();
                cursor = limit - c9;
            }
            cursor = limit_backward;
            return true;
        }

    }
}

