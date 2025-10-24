# หนังสือ: การพัฒนา Web Application ด้วย Functional Programming

> เรียนรู้การสร้าง Full-Stack Web Application แบบ Functional Programming ด้วย C# language-ext v5 และ TypeScript Effect-TS

---

## 📖 เกี่ยวกับหนังสือเล่มนี้

หนังสือเล่มนี้จะพาคุณเรียนรู้การพัฒนา **Full-Stack Web Application** โดยใช้ **Functional Programming** ตั้งแต่ต้นจนจบ ผ่านการสร้าง **Todo Application** จริง

คุณจะได้เรียนรู้:
- ✅ Functional Programming Principles
- ✅ Backend Development ด้วย **C# + language-ext v5**
- ✅ Frontend Development ด้วย **TypeScript + Effect-TS**
- ✅ Trait-based Architecture
- ✅ Effect Systems
- ✅ Type-safe Error Handling
- ✅ Testable Architecture
- ✅ Full-Stack Integration

---

## 🎯 เหมาะสำหรับ

- นักพัฒนาที่ต้องการเรียนรู้ Functional Programming จริงจัง
- นักพัฒนาที่เขียน Imperative มาตลอด อยากลองแนวใหม่
- ทีมที่ต้องการ architecture ที่ maintainable และ testable มากขึ้น
- นักศึกษาที่สนใจ Advanced Programming Paradigms

**ควรมีพื้นฐาน:**
- C# หรือ TypeScript พื้นฐาน
- ASP.NET Core หรือ React พื้นฐาน
- เข้าใจ Generic Types
- อยากเรียนรู้สิ่งใหม่!

---

## 📚 สารบัญ

### ภาคที่ 1: บทนำและพื้นฐาน

- **[บทที่ 1: ทำไมต้อง Functional Programming?](chapter-01.md)** ✅
  - ปัญหาของ Imperative Programming
  - ข้อดีของ Functional Programming
  - ภาพรวมของ Application ที่จะสร้าง

- **บทที่ 2: แนวคิดพื้นฐาน Functional Programming** (เร็วๆ นี้)
  - Pure Functions
  - Immutability
  - Function Composition
  - Monads และ Effects
  - Type Safety

### ภาคที่ 2: Backend Development (C# + language-ext v5)

- **บทที่ 3: แนะนำ language-ext v5**
- **บทที่ 4: Has<M, RT, T>.ask Pattern**
- **บทที่ 5: สร้าง Backend API ด้วย Capabilities**
- **บทที่ 6: Validation และ Error Handling**
- **บทที่ 7: Testing Backend**

### ภาคที่ 3: Frontend Development (TypeScript + Effect-TS)

- **บทที่ 8: แนะนำ Effect-TS**
- **บทที่ 9: สร้าง Frontend Architecture**
- **บทที่ 10: React Integration**
- **บทที่ 11: Validation และ Form Handling**
- **บทที่ 12: Testing Frontend**

### ภาคที่ 4: Full-Stack Integration

- **บทที่ 13: เชื่อมต่อ Backend และ Frontend**
- **บทที่ 14: Parallel Patterns**

### ภาคที่ 5: Advanced Topics

- **บทที่ 15: Specification Pattern**
- **บทที่ 16: Pagination Pattern**
- **บทที่ 17: Transaction Handling**
- **บทที่ 18: Best Practices**
- **บทที่ 19: Production Deployment**

### ภาคผนวก

- **ภาคผนวก A:** Troubleshooting Guide
- **ภาคผนวก B:** API Reference
- **ภาคผนวก C:** Code Examples
- **ภาคผนวก D:** Resources และ Further Reading

---

## 💻 Source Code

Source code ทั้งหมดของโปรเจคที่ใช้ในหนังสือ:

```bash
FP-Concepts/
├── TodoApp/                 # Backend (C# + language-ext v5)
├── todo-app-frontend/       # Frontend (TypeScript + Effect-TS)
└── book/                    # หนังสือ (Markdown)
```

### ดาวน์โหลดโปรเจค

```bash
git clone https://github.com/yourusername/FP-Concepts.git
cd FP-Concepts
```

### Quick Start

**Backend:**
```bash
cd TodoApp
dotnet restore
dotnet run
```

**Frontend:**
```bash
cd todo-app-frontend
npm install
npm run dev
```

ดู [QUICKSTART.md](../QUICKSTART.md) สำหรับคำแนะนำโดยละเอียด

---

## 🛠️ Technology Stack

**Backend:**
- ASP.NET Core 8.0
- language-ext v5.0.0-beta-54
- Entity Framework Core 9.0
- SQLite

**Frontend:**
- React 18
- TypeScript 5.2
- Effect-TS 3.10
- Vite 5

**Testing:**
- Backend: NUnit
- Frontend: Vitest + Testing Library

---

## 📊 สถิติ

- **จำนวนบท:** 19 บท + 4 ภาคผนวก
- **จำนวนหน้าโดยประมาณ:** ~230 หน้า
- **Backend Tests:** 14 tests ผ่านทั้งหมด ✅
- **Frontend Tests:** 29 tests ผ่านทั้งหมด ✅
- **ตัวอย่างโค้ด:** 100+ ตัวอย่าง
- **Diagrams:** 20+ ภาพประกอบ

---

## 🎓 วิธีการอ่าน

### สำหรับผู้เริ่มต้น

1. อ่านภาคที่ 1 เพื่อเข้าใจภาพรวม
2. ติดตั้งและรันโปรเจคตัวอย่าง
3. อ่านภาคที่ 2-3 พร้อมกับ code along
4. ทำแบบฝึกหัดท้ายบท

### สำหรับผู้ที่มีประสบการณ์

- สนใจ Backend? → กระโดดไปภาคที่ 2
- สนใจ Frontend? → กระโดดไปภาคที่ 3
- สนใจ Architecture? → อ่านบทที่ 4, 9, 15
- สนใจ Testing? → อ่านบทที่ 7, 12

---

## 🌟 จุดเด่นของหนังสือเล่มนี้

### 1. เรียนรู้จากโปรเจคจริง

ไม่ใช่แค่ทฤษฎี แต่เป็น **โปรเจคจริงที่รันได้** และมี **test ผ่านทั้งหมด**!

### 2. Full-Stack Functional Programming

เรียนรู้ **ทั้ง Backend และ Frontend** ด้วย Functional patterns ที่สอดคล้องกัน

### 3. Modern Architecture

- **Has<M, RT, T>.ask** pattern (language-ext v5)
- **Effect-TS** (Effect system ที่ทันสมัย)
- **Trait-based** testing

### 4. Production-Ready

โค้ดทั้งหมด:
- ✅ Type-safe
- ✅ Tested (43 tests รวม)
- ✅ Documented
- ✅ Following best practices

### 5. เปรียบเทียบ Imperative vs Functional

ทุกบทมีการเปรียบเทียบ **Before/After** เพื่อให้เห็นความแตกต่างชัดเจน

---

## 📝 แบบฝึกหัด

แต่ละบทมี:
- 🤔 **คำถามทบทวน** - ตรวจสอบความเข้าใจ
- 💻 **Coding Exercises** - ฝึกเขียนโค้ด
- 🏆 **Challenges** - โจทย์ยากสำหรับขยายความสามารถ

---

## 🤝 การมีส่วนร่วม

พบ typo หรือมีข้อเสนอแนะ?

1. เปิด Issue ที่ GitHub
2. Submit Pull Request
3. ติดต่อผู้เขียนโดยตรง

---

## 📞 ติดต่อ

- Email: [your-email]
- Twitter: [@yourtwitter]
- Discord: [discord-link]

---

## 📄 License

MIT License - ใช้งานได้อย่างอิสระ

---

## ขอบคุณ

หนังสือเล่มนี้ได้แรงบันดาลใจจาก:
- [louthy/language-ext](https://github.com/louthy/language-ext)
- [Effect-TS/effect](https://github.com/Effect-TS/effect)
- Functional Programming Community

---

## เริ่มต้นกันเลย

พร้อมแล้วหรือยัง? ไปกันที่ **[บทที่ 1: ทำไมต้อง Functional Programming?](chapter-01.md)**

หรือถ้าอยากรันโค้ดก่อน ดูที่ **[Quick Start Guide](../QUICKSTART.md)**

---

**มาเรียนรู้ Functional Programming ด้วยกัน!**
