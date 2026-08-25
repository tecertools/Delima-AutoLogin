/**
 * DELIMa Windows 11 Web UI — Sample Data & Catalog
 * Authentic Malaysian Primary School Rosters, Classes & Picture Password Symbols
 */

const DelimaData = {
  school: {
    name: "SK Seksyen 24 Shah Alam",
    code: "BBA8201",
    motto: "Berilmu, Beramal, Berbakti",
    crestText: "SK24",
    crestBg: "#007A3D",
    crestFg: "#FFFFFF",
    adminPassphrase: "", // Never store real passphrases in mock data
    labName: "Makmal Komputer Al-Khawarizmi (28 PC)"
  },

  symbolsCatalog: [
    { id: "singa", emoji: "🦁", name: "Singa", category: "haiwan" },
    { id: "roket", emoji: "🚀", name: "Roket", category: "kenderaan" },
    { id: "bola", emoji: "⚽", name: "Bola", category: "sukan" },
    { id: "epal", emoji: "🍎", name: "Epal", category: "makanan" },
    { id: "kucing", emoji: "🐱", name: "Kucing", category: "haiwan" },
    { id: "bintang", emoji: "🌟", name: "Bintang", category: "alam" },
    { id: "kereta", emoji: "🚗", name: "Kereta", category: "kenderaan" },
    { id: "aiskrim", emoji: "🍦", name: "Aiskrim", category: "makanan" },
    { id: "rama", emoji: "🦋", name: "Rama-rama", category: "haiwan" },
    { id: "bunga", emoji: "🌻", name: "Bunga", category: "alam" },
    { id: "kapal", emoji: "✈️", name: "Kapal Terbang", category: "kenderaan" },
    { id: "belon", emoji: "🎈", name: "Belon", category: "mainan" },
    { id: "strawberi", emoji: "🍓", name: "Strawberi", category: "makanan" },
    { id: "gajah", emoji: "🐘", name: "Gajah", category: "haiwan" },
    { id: "pelangi", emoji: "🌈", name: "Pelangi", category: "alam" },
    { id: "kura", emoji: "🐢", name: "Kura-kura", category: "haiwan" }
  ],

  classes: [
    { id: "1A", grade: 1, name: "1 Amanah", stage: "Tahap 1", studentCount: 8, icon: "🌱" },
    { id: "1B", grade: 1, name: "1 Bestari", stage: "Tahap 1", studentCount: 8, icon: "🌿" },
    { id: "2C", grade: 2, name: "2 Cemerlang", stage: "Tahap 1", studentCount: 8, icon: "⭐" },
    { id: "3B", grade: 3, name: "3 Bijak", stage: "Tahap 1", studentCount: 8, icon: "💡" },
    { id: "4D", grade: 4, name: "4 Dinamik", stage: "Tahap 2", studentCount: 8, icon: "🚀" },
    { id: "5E", grade: 5, name: "5 Elit", stage: "Tahap 2", studentCount: 8, icon: "🎯" },
    { id: "6F", grade: 6, name: "6 Fikir", stage: "Tahap 2", studentCount: 8, icon: "🎓" }
  ],

  students: [
    // 1 Amanah
    { id: "m-101", name: "Aisyah Humaira binti Ahmad", classId: "1A", email: "m-10148291@moe-dl.edu.my", avatar: "👧", passSymbols: ["🦁", "🌟", "🍎"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-102", name: "Muhammad Adam Rayyan bin Mohd Rizal", classId: "1A", email: "m-10148292@moe-dl.edu.my", avatar: "👦", passSymbols: ["🚀", "⚽", "🚗"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-103", name: "Nur Damia Qaisara binti Khairul", classId: "1A", email: "m-10148293@moe-dl.edu.my", avatar: "👧", passSymbols: ["🦋", "🌻", "🍦"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-104", name: "Amirul Haziq bin Shahrul", classId: "1A", email: "m-10148294@moe-dl.edu.my", avatar: "👦", passSymbols: ["🦁", "🚗", "⚽"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-105", name: "Siti Nurhaliza binti Zulkifli", classId: "1A", email: "m-10148295@moe-dl.edu.my", avatar: "👧", passSymbols: ["🌟", "🍓", "🎈"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-106", name: "Danish Irfan bin Mohd Fauzi", classId: "1A", email: "m-10148296@moe-dl.edu.my", avatar: "👦", passSymbols: ["✈️", "🚀", "🐘"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-107", name: "Hana Maisarah binti Azman", classId: "1A", email: "m-10148297@moe-dl.edu.my", avatar: "👧", passSymbols: ["🐱", "🌈", "🍦"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-108", name: "Haris Danial bin Kamaruddin", classId: "1A", email: "m-10148298@moe-dl.edu.my", avatar: "👦", passSymbols: ["🐢", "⚽", "🌟"], status: "assigned", passwordText: "MOCK_PASSWORD" },

    // 1 Bestari
    { id: "m-109", name: "Muhammad Aqil Zafri bin Zaidi", classId: "1B", email: "m-10148301@moe-dl.edu.my", avatar: "👦", passSymbols: ["🦁", "🚀", "🌟"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-110", name: "Nur Alya Sofea binti Rosli", classId: "1B", email: "m-10148302@moe-dl.edu.my", avatar: "👧", passSymbols: ["🦋", "🍎", "🎈"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-111", name: "Farhan Daniel bin Imran", classId: "1B", email: "m-10148303@moe-dl.edu.my", avatar: "👦", passSymbols: ["🚗", "⚽", "✈️"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-112", name: "Maryam Jameelah binti Luqman", classId: "1B", email: "m-10148304@moe-dl.edu.my", avatar: "👧", passSymbols: ["🌻", "🌈", "🐱"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-113", name: "Zarif Iman bin Khairuddin", classId: "1B", email: "m-10148305@moe-dl.edu.my", avatar: "👦", passSymbols: ["🚀", "🐘", "🌟"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-114", name: "Nurin Qistina binti Mahathir", classId: "1B", email: "m-10148306@moe-dl.edu.my", avatar: "👧", passSymbols: ["🍦", "🍓", "🎈"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-115", name: "Aryan Mikhail bin Ridzuan", classId: "1B", email: "m-10148307@moe-dl.edu.my", avatar: "👦", passSymbols: ["🦁", "⚽", "🚗"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-116", name: "Khadeeja binti Mohd Hanif", classId: "1B", email: "m-10148308@moe-dl.edu.my", avatar: "👧", passSymbols: ["🐱", "🦋", "🌟"], status: "assigned", passwordText: "MOCK_PASSWORD" },

    // 2 Cemerlang
    { id: "m-201", name: "Ahmad Muhaimin bin Saiful", classId: "2C", email: "m-10148401@moe-dl.edu.my", avatar: "👦", passSymbols: ["🚀", "🌟", "⚽"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-202", name: "Zara Elisya binti Norazam", classId: "2C", email: "m-10148402@moe-dl.edu.my", avatar: "👧", passSymbols: ["🍓", "🍦", "🎈"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-203", name: "Izzat Naufal bin Mohd Nizam", classId: "2C", email: "m-10148403@moe-dl.edu.my", avatar: "👦", passSymbols: ["🦁", "🚗", "✈️"], status: "assigned", passwordText: "MOCK_PASSWORD" },
    { id: "m-204", name: "Nisa Batrisya binti Hairul", classId: "2C", email: "m-10148404@moe-dl.edu.my", avatar: "👧", passSymbols: ["🌻", "🦋", "🌈"], status: "assigned", passwordText: "MOCK_PASSWORD" }
  ]
};
