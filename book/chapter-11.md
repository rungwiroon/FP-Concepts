# บทที่ 11: Validation และ Form Handling

> Advanced Form Patterns และ Schema Validation ด้วย Effect-TS

---

## เนื้อหาในบทนี้

- Schema Validation with Effect Schema
- Complex Form Validation
- Multi-step Forms (Wizard Pattern)
- File Upload Handling
- Dynamic Array Fields
- Nested Object Forms
- Cross-field Validation
- Async Validation Patterns
- Custom Validators
- Form Testing
- Best Practices

---

## 11.1 Schema Validation with Effect Schema

### 11.1.1 ทำไมต้องใช้ Schema Validation?

ในบทที่ 10 เราเขียน validation แบบ manual:

```typescript
// ❌ Manual validation - verbose และ repetitive
function validateEmail(email: string): Either<string, ValidationError> {
  if (email.trim() === '') {
    return Either.left({ field: 'email', message: 'Required' });
  }
  if (!email.includes('@')) {
    return Either.left({ field: 'email', message: 'Invalid email' });
  }
  return Either.right(email);
}
```

**ปัญหา:**
1. **Verbose** - ต้องเขียนเช็คทุก field
2. **Repetitive** - validation rules ซ้ำกัน
3. **ไม่ Reusable** - ยากต่อการนำกลับมาใช้
4. **Maintenance** - แก้ไขยาก

**Solution: Effect Schema**

Effect Schema ช่วยให้เรา:
1. **Define Schema** - กำหนด structure และ validation ที่เดียว
2. **Automatic Validation** - validate อัตโนมัติตาม schema
3. **Type Generation** - สร้าง TypeScript types จาก schema
4. **Composable** - ประกอบ schemas ได้

### 11.1.2 Getting Started with Effect Schema

**Installation:**

```bash
npm install @effect/schema
```

**Basic Schema:**

Effect Schema ใช้ **declarative style** ในการกำหนด data structure และ validation rules

**แนวคิด:**

```
Schema = Data Structure + Validation Rules + Type Information

Schema.string → string type + string validation
Schema.number → number type + number validation
Schema.struct → object type + field validation
```

**Example:**

```typescript
import { Schema } from '@effect/schema';

// ✅ Define schema - ชัดเจนและกระชับ
const EmailSchema = Schema.string.pipe(
  Schema.minLength(1, { message: () => 'Email is required' }),
  Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/, {
    message: () => 'Invalid email format'
  })
);

// Infer TypeScript type
type Email = Schema.Schema.To<typeof EmailSchema>;
// type Email = string
```

**Benefits:**
- ✅ Declarative - อ่านง่าย เข้าใจง่าย
- ✅ Type-safe - TypeScript types auto-generated
- ✅ Reusable - นำ schema กลับมาใช้ได้
- ✅ Composable - ประกอบ schemas ซับซ้อนได้

### 11.1.3 Schema Types

Effect Schema มี built-in types มากมาย:

**Primitive Types:**

```typescript
import { Schema } from '@effect/schema';

// String
const NameSchema = Schema.string;

// Number
const AgeSchema = Schema.number;

// Boolean
const IsActiveSchema = Schema.boolean;

// Date
const BirthDateSchema = Schema.Date;

// Literal
const StatusSchema = Schema.literal('active', 'inactive', 'pending');
```

**String Validations:**

```typescript
// Min/Max length
const UsernameSchema = Schema.string.pipe(
  Schema.minLength(3, { message: () => 'Username must be at least 3 characters' }),
  Schema.maxLength(20, { message: () => 'Username must be less than 20 characters' })
);

// Pattern (regex)
const PhoneSchema = Schema.string.pipe(
  Schema.pattern(/^[0-9]{10}$/, {
    message: () => 'Phone must be 10 digits'
  })
);

// Email
const EmailSchema = Schema.string.pipe(
  Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/, {
    message: () => 'Invalid email format'
  })
);

// URL
const WebsiteSchema = Schema.string.pipe(
  Schema.pattern(/^https?:\/\/.+/, {
    message: () => 'Must be a valid URL'
  })
);
```

**Number Validations:**

```typescript
// Min/Max value
const PriceSchema = Schema.number.pipe(
  Schema.greaterThan(0, { message: () => 'Price must be positive' }),
  Schema.lessThanOrEqualTo(1000000, { message: () => 'Price too high' })
);

// Integer
const QuantitySchema = Schema.number.pipe(
  Schema.int({ message: () => 'Quantity must be an integer' }),
  Schema.greaterThanOrEqualTo(1, { message: () => 'Quantity must be at least 1' })
);

// Between
const RatingSchema = Schema.number.pipe(
  Schema.between(1, 5, { message: () => 'Rating must be between 1 and 5' })
);
```

**Array Validations:**

```typescript
// Array of strings
const TagsSchema = Schema.array(Schema.string);

// Array with min/max length
const TodoListSchema = Schema.array(Schema.string).pipe(
  Schema.minItems(1, { message: () => 'Must have at least 1 todo' }),
  Schema.maxItems(10, { message: () => 'Maximum 10 todos allowed' })
);
```

### 11.1.4 Struct Schemas (Objects)

Struct schemas define object structures:

**Simple Object:**

```typescript
import { Schema } from '@effect/schema';

// Define user schema
const UserSchema = Schema.struct({
  id: Schema.string,
  name: Schema.string.pipe(
    Schema.minLength(1, { message: () => 'Name is required' })
  ),
  email: Schema.string.pipe(
    Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/, {
      message: () => 'Invalid email'
    })
  ),
  age: Schema.number.pipe(
    Schema.int(),
    Schema.between(18, 120, { message: () => 'Age must be between 18 and 120' })
  ),
  isActive: Schema.boolean
});

// Infer TypeScript type
type User = Schema.Schema.To<typeof UserSchema>;
/*
type User = {
  id: string;
  name: string;
  email: string;
  age: number;
  isActive: boolean;
}
*/
```

**Optional Fields:**

```typescript
const UserProfileSchema = Schema.struct({
  name: Schema.string,
  bio: Schema.optional(Schema.string),  // Optional field
  website: Schema.optional(
    Schema.string.pipe(
      Schema.pattern(/^https?:\/\/.+/)
    )
  )
});

type UserProfile = Schema.Schema.To<typeof UserProfileSchema>;
/*
type UserProfile = {
  name: string;
  bio?: string;
  website?: string;
}
*/
```

**Nested Objects:**

```typescript
const AddressSchema = Schema.struct({
  street: Schema.string,
  city: Schema.string,
  zipCode: Schema.string.pipe(
    Schema.pattern(/^[0-9]{5}$/, {
      message: () => 'Zip code must be 5 digits'
    })
  )
});

const UserWithAddressSchema = Schema.struct({
  name: Schema.string,
  email: Schema.string,
  address: AddressSchema  // Nested object
});

type UserWithAddress = Schema.Schema.To<typeof UserWithAddressSchema>;
/*
type UserWithAddress = {
  name: string;
  email: string;
  address: {
    street: string;
    city: string;
    zipCode: string;
  };
}
*/
```

### 11.1.5 Decoding and Validation

Schema ไม่ได้แค่ define types แต่ยังสามารถ **decode** และ **validate** ข้อมูลได้

**Decoding Pattern:**

```
Unknown Data → Schema.decode → Validated Data
     ↓                              ↓
  any type                    Correct type

If invalid → Effect.fail(ParseError)
If valid → Effect.succeed(validData)
```

**Example:**

```typescript
import { Schema } from '@effect/schema';
import { Effect } from 'effect';

const UserSchema = Schema.struct({
  name: Schema.string.pipe(Schema.minLength(1)),
  age: Schema.number.pipe(Schema.int(), Schema.greaterThan(0))
});

// Decode unknown data
const decodeUser = Schema.decode(UserSchema);

// ✅ Valid data
const validInput = { name: 'John', age: 25 };

Effect.runPromise(decodeUser(validInput))
  .then(user => console.log('Valid:', user))
  .catch(error => console.error('Error:', error));
// Output: Valid: { name: 'John', age: 25 }

// ❌ Invalid data
const invalidInput = { name: '', age: -5 };

Effect.runPromise(decodeUser(invalidInput))
  .then(user => console.log('Valid:', user))
  .catch(error => console.error('Error:', error));
// Output: Error: ParseError...
```

**Parse Errors:**

เมื่อ validation ล้มเหลว Schema จะ return `ParseError` ที่มีข้อมูลครบถ้วน:

```typescript
import { Schema, ParseResult } from '@effect/schema';
import { Effect, Either } from 'effect';

const validate = <A, I>(schema: Schema.Schema<A, I>) => (input: unknown) =>
  Effect.either(Schema.decode(schema)(input));

const result = await Effect.runPromise(validate(UserSchema)(invalidInput));

Either.match(result, {
  onLeft: (error) => {
    // ParseError contains detailed information
    console.log('Validation failed:');
    console.log(ParseResult.TreeFormatter.formatErrors(error.errors));
  },
  onRight: (data) => {
    console.log('Valid data:', data);
  }
});
```

---

## 11.2 Complex Form Validation

### 11.2.1 Todo Form with Schema

มาสร้าง Todo form ที่ซับซ้อนขึ้นด้วย Effect Schema

**Requirements:**
1. Title - required, 3-200 characters
2. Description - optional, max 1000 characters
3. Priority - low/medium/high
4. Due date - must be in the future
5. Tags - array of strings, max 5 tags

**Schema Definition:**

```typescript
import { Schema } from '@effect/schema';

// Priority enum
const PrioritySchema = Schema.literal('low', 'medium', 'high');

// Tag schema
const TagSchema = Schema.string.pipe(
  Schema.minLength(1, { message: () => 'Tag cannot be empty' }),
  Schema.maxLength(20, { message: () => 'Tag too long (max 20 chars)' })
);

// Todo form schema
const TodoFormSchema = Schema.struct({
  title: Schema.string.pipe(
    Schema.minLength(3, {
      message: () => 'Title must be at least 3 characters'
    }),
    Schema.maxLength(200, {
      message: () => 'Title too long (max 200 characters)'
    })
  ),

  description: Schema.optional(
    Schema.string.pipe(
      Schema.maxLength(1000, {
        message: () => 'Description too long (max 1000 characters)'
      })
    )
  ),

  priority: PrioritySchema,

  dueDate: Schema.Date.pipe(
    Schema.filter(date => date > new Date(), {
      message: () => 'Due date must be in the future'
    })
  ),

  tags: Schema.array(TagSchema).pipe(
    Schema.maxItems(5, {
      message: () => 'Maximum 5 tags allowed'
    })
  )
});

// Infer types
type TodoFormInput = Schema.Schema.From<typeof TodoFormSchema>;
type TodoForm = Schema.Schema.To<typeof TodoFormSchema>;

/*
type TodoForm = {
  title: string;
  description?: string;
  priority: 'low' | 'medium' | 'high';
  dueDate: Date;
  tags: string[];
}
*/
```

### 11.2.2 Using Schema in useForm Hook

เราจะ integrate Schema กับ `useForm` hook จากบทที่ 10

**แนวคิด:**

```
Form input (unknown type)
   ↓
Schema.decode → Validate
   ↓
If valid → Effect.succeed(validData) → onSubmit
If invalid → Effect.fail(errors) → Show errors
```

**Integration:**

```typescript
import { useState, useCallback } from 'react';
import { Schema } from '@effect/schema';
import { Effect, Either } from 'effect';

interface UseFormSchemaOptions<A, I> {
  schema: Schema.Schema<A, I>;
  initialValues: I;
  onSubmit: (values: A) => Effect.Effect<void, Error, never>;
}

export function useFormSchema<A, I>({
  schema,
  initialValues,
  onSubmit
}: UseFormSchemaOptions<A, I>) {
  const [values, setValues] = useState<I>(initialValues);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const setValue = useCallback(
    <K extends keyof I>(field: K, value: I[K]) => {
      setValues(prev => ({ ...prev, [field]: value }));
      // Clear error when user changes value
      setErrors(prev => {
        const next = { ...prev };
        delete next[field as string];
        return next;
      });
    },
    []
  );

  const handleSubmit = useCallback(
    async (e?: React.FormEvent) => {
      if (e) e.preventDefault();

      setSubmitting(true);
      setErrors({});

      const decode = Schema.decode(schema);

      // ✅ Functional: Use Effect.matchEffect
      await Effect.runPromise(
        decode(values).pipe(
          Effect.matchEffect({
            onFailure: (parseError) =>
              Effect.sync(() => {
                // Extract field errors from ParseError
                const fieldErrors: Record<string, string> = {};

                // Format errors
                parseError.errors.forEach(error => {
                  const path = error.path.join('.');
                  fieldErrors[path] = error.message;
                });

                setErrors(fieldErrors);
                setSubmitting(false);
              }),
            onSuccess: (validData) =>
              Effect.gen(function* (_) {
                yield* _(onSubmit(validData));

                // Reset form on success
                setValues(initialValues);
                setSubmitting(false);
              })
          })
        )
      ).catch(error => {
        setErrors({ _form: (error as Error).message });
        setSubmitting(false);
      });
    },
    [values, schema, onSubmit, initialValues]
  );

  const reset = useCallback(() => {
    setValues(initialValues);
    setErrors({});
    setSubmitting(false);
  }, [initialValues]);

  return {
    values,
    errors,
    submitting,
    setValue,
    handleSubmit,
    reset
  };
}
```

### 11.2.3 Todo Form Component

ตอนนี้เราสามารถใช้ `useFormSchema` กับ `TodoFormSchema` ได้

```typescript
import React from 'react';
import { Effect } from 'effect';
import { useFormSchema } from '@/hooks/useFormSchema';
import { TodoFormSchema } from '@/schemas/TodoSchema';
import { useTodoStore } from '@/contexts/TodoStoreContext';
import { useEffectLayer } from '@/contexts/EffectContext';
import clsx from 'clsx';

export function CreateTodoForm() {
  const store = useTodoStore();
  const layer = useEffectLayer();

  const form = useFormSchema({
    schema: TodoFormSchema,
    initialValues: {
      title: '',
      description: '',
      priority: 'medium' as const,
      dueDate: new Date(Date.now() + 24 * 60 * 60 * 1000), // Tomorrow
      tags: []
    },
    onSubmit: (values) =>
      Effect.gen(function* (_) {
        // Create todo with validated data
        yield* _(store.create(values.title));
      }).pipe(Effect.provide(layer))
  });

  return (
    <form onSubmit={form.handleSubmit} className="todo-form">
      {/* Title */}
      <div className="form-group">
        <label htmlFor="title">Title *</label>
        <input
          id="title"
          type="text"
          value={form.values.title}
          onChange={e => form.setValue('title', e.target.value)}
          className={clsx('form-control', {
            'is-invalid': form.errors.title
          })}
          disabled={form.submitting}
        />
        {form.errors.title && (
          <div className="error-message">{form.errors.title}</div>
        )}
      </div>

      {/* Description */}
      <div className="form-group">
        <label htmlFor="description">Description</label>
        <textarea
          id="description"
          value={form.values.description || ''}
          onChange={e => form.setValue('description', e.target.value)}
          className={clsx('form-control', {
            'is-invalid': form.errors.description
          })}
          rows={4}
          disabled={form.submitting}
        />
        {form.errors.description && (
          <div className="error-message">{form.errors.description}</div>
        )}
      </div>

      {/* Priority */}
      <div className="form-group">
        <label htmlFor="priority">Priority *</label>
        <select
          id="priority"
          value={form.values.priority}
          onChange={e =>
            form.setValue('priority', e.target.value as 'low' | 'medium' | 'high')
          }
          className="form-control"
          disabled={form.submitting}
        >
          <option value="low">Low</option>
          <option value="medium">Medium</option>
          <option value="high">High</option>
        </select>
      </div>

      {/* Due Date */}
      <div className="form-group">
        <label htmlFor="dueDate">Due Date *</label>
        <input
          id="dueDate"
          type="date"
          value={form.values.dueDate.toISOString().split('T')[0]}
          onChange={e => form.setValue('dueDate', new Date(e.target.value))}
          className={clsx('form-control', {
            'is-invalid': form.errors.dueDate
          })}
          disabled={form.submitting}
        />
        {form.errors.dueDate && (
          <div className="error-message">{form.errors.dueDate}</div>
        )}
      </div>

      {/* Tags (will add in next section) */}

      {/* Form-level error */}
      {form.errors._form && (
        <div className="alert alert-danger">{form.errors._form}</div>
      )}

      {/* Actions */}
      <div className="form-actions">
        <button
          type="submit"
          className="btn btn-primary"
          disabled={form.submitting}
        >
          {form.submitting ? 'Creating...' : 'Create Todo'}
        </button>

        <button
          type="button"
          className="btn btn-secondary"
          onClick={form.reset}
          disabled={form.submitting}
        >
          Reset
        </button>
      </div>
    </form>
  );
}
```

**Benefits:**
- ✅ No manual validation code
- ✅ Type-safe form values
- ✅ Automatic error messages
- ✅ Easy to extend

---

## 11.3 Dynamic Array Fields

### 11.3.1 Managing Tag Array

Tags เป็น array ที่ user สามารถเพิ่ม/ลบได้ dynamic

**Requirements:**
1. Add new tag
2. Remove existing tag
3. Validate each tag
4. Max 5 tags

**Array Field Component:**

```typescript
import React, { useState } from 'react';

interface TagsInputProps {
  value: string[];
  onChange: (tags: string[]) => void;
  error?: string;
  disabled?: boolean;
}

export function TagsInput({ value, onChange, error, disabled }: TagsInputProps) {
  const [input, setInput] = useState('');

  const addTag = () => {
    const trimmed = input.trim();

    if (trimmed === '') return;

    // Check if already exists
    if (value.includes(trimmed)) {
      alert('Tag already exists');
      return;
    }

    // Check max limit
    if (value.length >= 5) {
      alert('Maximum 5 tags allowed');
      return;
    }

    // Add tag
    onChange([...value, trimmed]);
    setInput('');
  };

  const removeTag = (index: number) => {
    onChange(value.filter((_, i) => i !== index));
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addTag();
    }
  };

  return (
    <div className="tags-input">
      {/* Display tags */}
      <div className="tags-list">
        {value.map((tag, index) => (
          <div key={index} className="tag-item">
            <span>{tag}</span>
            <button
              type="button"
              onClick={() => removeTag(index)}
              disabled={disabled}
              className="tag-remove"
            >
              ×
            </button>
          </div>
        ))}
      </div>

      {/* Input for new tag */}
      <div className="tags-input-field">
        <input
          type="text"
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Add tag and press Enter"
          disabled={disabled || value.length >= 5}
        />
        <button
          type="button"
          onClick={addTag}
          disabled={disabled || value.length >= 5}
        >
          Add
        </button>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="help-text">
        {value.length}/5 tags ({5 - value.length} remaining)
      </div>
    </div>
  );
}
```

**Usage in Form:**

```typescript
export function CreateTodoForm() {
  // ... other form code

  return (
    <form onSubmit={form.handleSubmit}>
      {/* ... other fields */}

      {/* Tags */}
      <div className="form-group">
        <label>Tags</label>
        <TagsInput
          value={form.values.tags}
          onChange={tags => form.setValue('tags', tags)}
          error={form.errors.tags}
          disabled={form.submitting}
        />
      </div>

      {/* ... actions */}
    </form>
  );
}
```

---

## 11.4 Multi-step Forms (Wizard Pattern)

### 11.4.1 Wizard Concept

Multi-step forms แบ่งฟอร์มยาวๆ เป็นหลายขั้นตอน เพื่อให้ UX ดีขึ้น

**Use Cases:**
- Registration forms
- Checkout flows
- Survey/questionnaire
- Onboarding processes

**Pattern:**

```
Step 1 (Personal Info) → Validate → Save
   ↓
Step 2 (Contact Info) → Validate → Save
   ↓
Step 3 (Preferences) → Validate → Save
   ↓
Review & Submit → All data validated → Submit
```

**Benefits:**
- ✅ Better UX - ไม่ overwhelm user
- ✅ Progressive validation - validate ทีละ step
- ✅ Save progress - บันทึกความคืบหน้า
- ✅ Review before submit - ตรวจสอบก่อนส่ง

### 11.4.2 Wizard State Management

เราจะสร้าง `useWizard` hook เพื่อจัดการ multi-step forms

**Wizard State:**

```typescript
interface WizardState<T extends Record<string, any>> {
  currentStep: number;
  steps: WizardStep<T>[];
  data: Partial<T>;
  completed: Set<number>;
}

interface WizardStep<T> {
  id: string;
  title: string;
  schema: Schema.Schema<any>;
  fields: (keyof T)[];
}
```

**useWizard Hook:**

```typescript
import { useState, useCallback } from 'react';
import { Schema } from '@effect/schema';
import { Effect } from 'effect';

interface WizardStep<T> {
  id: string;
  title: string;
  description?: string;
  schema: Schema.Schema<any>;
}

interface UseWizardOptions<T> {
  steps: WizardStep<T>[];
  initialData?: Partial<T>;
  onComplete: (data: T) => Effect.Effect<void, Error, never>;
}

export function useWizard<T extends Record<string, any>>({
  steps,
  initialData = {},
  onComplete
}: UseWizardOptions<T>) {
  const [currentStep, setCurrentStep] = useState(0);
  const [data, setData] = useState<Partial<T>>(initialData);
  const [completed, setCompleted] = useState<Set<number>>(new Set());
  const [submitting, setSubmitting] = useState(false);

  const currentStepConfig = steps[currentStep];
  const isFirstStep = currentStep === 0;
  const isLastStep = currentStep === steps.length - 1;
  const canGoNext = completed.has(currentStep);

  /**
   * Update field value
   */
  const setValue = useCallback(
    <K extends keyof T>(field: K, value: T[K]) => {
      setData(prev => ({ ...prev, [field]: value }));
    },
    []
  );

  /**
   * Validate current step
   */
  const validateStep = useCallback(
    async (stepData: unknown) => {
      const decode = Schema.decode(currentStepConfig.schema);

      return Effect.runPromise(
        Effect.either(decode(stepData))
      );
    },
    [currentStepConfig]
  );

  /**
   * Go to next step
   */
  const next = useCallback(async () => {
    // Validate current step first
    const result = await validateStep(data);

    return Effect.runPromise(
      result.pipe(
        Effect.matchEffect({
          onFailure: (errors) =>
            Effect.fail(new Error('Validation failed')),
          onSuccess: () =>
            Effect.sync(() => {
              // Mark step as completed
              setCompleted(prev => new Set([...prev, currentStep]));

              // Go to next step
              if (!isLastStep) {
                setCurrentStep(prev => prev + 1);
              }
            })
        })
      )
    );
  }, [currentStep, data, validateStep, isLastStep]);

  /**
   * Go to previous step
   */
  const previous = useCallback(() => {
    if (!isFirstStep) {
      setCurrentStep(prev => prev - 1);
    }
  }, [isFirstStep]);

  /**
   * Go to specific step
   */
  const goToStep = useCallback((step: number) => {
    if (step >= 0 && step < steps.length) {
      // Can only go to completed steps or next step
      if (completed.has(step - 1) || step === 0) {
        setCurrentStep(step);
      }
    }
  }, [steps.length, completed]);

  /**
   * Submit final form
   */
  const submit = useCallback(async () => {
    setSubmitting(true);

    try {
      await Effect.runPromise(onComplete(data as T));
    } finally {
      setSubmitting(false);
    }
  }, [data, onComplete]);

  /**
   * Reset wizard
   */
  const reset = useCallback(() => {
    setCurrentStep(0);
    setData(initialData);
    setCompleted(new Set());
    setSubmitting(false);
  }, [initialData]);

  return {
    // State
    currentStep,
    currentStepConfig,
    data,
    completed,
    submitting,

    // Status
    isFirstStep,
    isLastStep,
    canGoNext,
    progress: ((completed.size + 1) / steps.length) * 100,

    // Actions
    setValue,
    next,
    previous,
    goToStep,
    submit,
    reset
  };
}
```

### 11.4.3 Multi-step Form Example

มาสร้าง user registration wizard ที่มี 3 steps:

**Step 1: Personal Info**

```typescript
import { Schema } from '@effect/schema';

const PersonalInfoSchema = Schema.struct({
  firstName: Schema.string.pipe(
    Schema.minLength(1, { message: () => 'First name is required' })
  ),
  lastName: Schema.string.pipe(
    Schema.minLength(1, { message: () => 'Last name is required' })
  ),
  birthDate: Schema.Date.pipe(
    Schema.filter(
      date => {
        const age = (Date.now() - date.getTime()) / (1000 * 60 * 60 * 24 * 365);
        return age >= 18;
      },
      { message: () => 'Must be at least 18 years old' }
    )
  )
});
```

**Step 2: Contact Info**

```typescript
const ContactInfoSchema = Schema.struct({
  email: Schema.string.pipe(
    Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/, {
      message: () => 'Invalid email'
    })
  ),
  phone: Schema.string.pipe(
    Schema.pattern(/^[0-9]{10}$/, {
      message: () => 'Phone must be 10 digits'
    })
  ),
  address: Schema.struct({
    street: Schema.string.pipe(Schema.minLength(1)),
    city: Schema.string.pipe(Schema.minLength(1)),
    zipCode: Schema.string.pipe(
      Schema.pattern(/^[0-9]{5}$/, {
        message: () => 'Zip code must be 5 digits'
      })
    )
  })
});
```

**Step 3: Preferences**

```typescript
const PreferencesSchema = Schema.struct({
  newsletter: Schema.boolean,
  notifications: Schema.struct({
    email: Schema.boolean,
    sms: Schema.boolean,
    push: Schema.boolean
  }),
  interests: Schema.array(Schema.string).pipe(
    Schema.minItems(1, { message: () => 'Select at least 1 interest' })
  )
});
```

**Wizard Component:**

```typescript
import React from 'react';
import { Effect } from 'effect';
import { useWizard } from '@/hooks/useWizard';
import { useEffectLayer } from '@/contexts/EffectContext';

interface RegistrationData {
  // Personal Info
  firstName: string;
  lastName: string;
  birthDate: Date;

  // Contact Info
  email: string;
  phone: string;
  address: {
    street: string;
    city: string;
    zipCode: string;
  };

  // Preferences
  newsletter: boolean;
  notifications: {
    email: boolean;
    sms: boolean;
    push: boolean;
  };
  interests: string[];
}

export function RegistrationWizard() {
  const layer = useEffectLayer();

  const wizard = useWizard<RegistrationData>({
    steps: [
      {
        id: 'personal',
        title: 'Personal Information',
        description: 'Tell us about yourself',
        schema: PersonalInfoSchema
      },
      {
        id: 'contact',
        title: 'Contact Information',
        description: 'How can we reach you?',
        schema: ContactInfoSchema
      },
      {
        id: 'preferences',
        title: 'Preferences',
        description: 'Customize your experience',
        schema: PreferencesSchema
      }
    ],
    initialData: {
      newsletter: true,
      notifications: {
        email: true,
        sms: false,
        push: true
      },
      interests: []
    },
    onComplete: (data) =>
      Effect.gen(function* (_) {
        // Submit registration
        console.log('Submitting registration:', data);
        // Call API here
      }).pipe(Effect.provide(layer))
  });

  return (
    <div className="wizard">
      {/* Progress Bar */}
      <div className="wizard-progress">
        <div
          className="wizard-progress-bar"
          style={{ width: `${wizard.progress}%` }}
        />
      </div>

      {/* Step Indicators */}
      <div className="wizard-steps">
        {wizard.steps.map((step, index) => (
          <div
            key={step.id}
            className={clsx('wizard-step', {
              active: index === wizard.currentStep,
              completed: wizard.completed.has(index)
            })}
            onClick={() => wizard.goToStep(index)}
          >
            <div className="step-number">{index + 1}</div>
            <div className="step-title">{step.title}</div>
          </div>
        ))}
      </div>

      {/* Current Step Content */}
      <div className="wizard-content">
        <h2>{wizard.currentStepConfig.title}</h2>
        {wizard.currentStepConfig.description && (
          <p>{wizard.currentStepConfig.description}</p>
        )}

        {/* Render step-specific form */}
        {wizard.currentStep === 0 && (
          <PersonalInfoStep
            data={wizard.data}
            onChange={wizard.setValue}
          />
        )}

        {wizard.currentStep === 1 && (
          <ContactInfoStep
            data={wizard.data}
            onChange={wizard.setValue}
          />
        )}

        {wizard.currentStep === 2 && (
          <PreferencesStep
            data={wizard.data}
            onChange={wizard.setValue}
          />
        )}
      </div>

      {/* Navigation */}
      <div className="wizard-actions">
        <button
          onClick={wizard.previous}
          disabled={wizard.isFirstStep}
          className="btn btn-secondary"
        >
          Previous
        </button>

        {!wizard.isLastStep && (
          <button
            onClick={wizard.next}
            className="btn btn-primary"
          >
            Next
          </button>
        )}

        {wizard.isLastStep && (
          <button
            onClick={wizard.submit}
            disabled={wizard.submitting || !wizard.canGoNext}
            className="btn btn-success"
          >
            {wizard.submitting ? 'Submitting...' : 'Complete Registration'}
          </button>
        )}
      </div>
    </div>
  );
}
```

---

## 11.5 File Upload Handling

### 11.5.1 File Upload with Effect

File uploads ต้องจัดการหลายอย่าง:
1. File validation (type, size)
2. Upload progress
3. Error handling
4. Preview

**File Schema:**

```typescript
import { Schema } from '@effect/schema';

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB

const FileSchema = Schema.instanceof(File).pipe(
  Schema.filter(
    (file) => file.size <= MAX_FILE_SIZE,
    {
      message: () => `File size must be less than ${MAX_FILE_SIZE / 1024 / 1024}MB`
    }
  ),
  Schema.filter(
    (file) => ['image/jpeg', 'image/png', 'image/gif'].includes(file.type),
    {
      message: () => 'File must be an image (JPEG, PNG, or GIF)'
    }
  )
);

const ProfileFormSchema = Schema.struct({
  name: Schema.string.pipe(Schema.minLength(1)),
  avatar: Schema.optional(FileSchema)
});
```

### 11.5.2 File Upload Service

สร้าง service สำหรับ upload files:

```typescript
import { Context, Effect, Stream } from 'effect';

export interface UploadProgress {
  loaded: number;
  total: number;
  percentage: number;
}

export interface FileUploadService {
  readonly upload: (
    file: File,
    onProgress?: (progress: UploadProgress) => void
  ) => Effect.Effect<string, Error, never>; // Returns URL
}

export const FileUploadService = Context.GenericTag<FileUploadService>(
  '@services/FileUploadService'
);
```

**Implementation:**

```typescript
import { Effect, Layer } from 'effect';
import { FileUploadService, type UploadProgress } from '@/services/FileUploadService';

export const FileUploadServiceLive = Layer.succeed(
  FileUploadService,
  FileUploadService.of({
    upload: (file, onProgress) =>
      Effect.tryPromise({
        try: () =>
          new Promise<string>((resolve, reject) => {
            const formData = new FormData();
            formData.append('file', file);

            const xhr = new XMLHttpRequest();

            // Progress tracking
            xhr.upload.addEventListener('progress', (e) => {
              if (e.lengthComputable && onProgress) {
                onProgress({
                  loaded: e.loaded,
                  total: e.total,
                  percentage: Math.round((e.loaded / e.total) * 100)
                });
              }
            });

            // Success
            xhr.addEventListener('load', () => {
              if (xhr.status >= 200 && xhr.status < 300) {
                const response = JSON.parse(xhr.responseText);
                resolve(response.url);
              } else {
                reject(new Error(`Upload failed: ${xhr.statusText}`));
              }
            });

            // Error
            xhr.addEventListener('error', () => {
              reject(new Error('Upload failed'));
            });

            // Send request
            xhr.open('POST', '/api/upload');
            xhr.send(formData);
          }),
        catch: (error) => new Error(`Failed to upload file: ${error}`)
      })
  })
);
```

### 11.5.3 File Upload Component

```typescript
import React, { useState } from 'react';
import { Effect } from 'effect';
import { FileUploadService, type UploadProgress } from '@/services/FileUploadService';
import { useEffectLayer } from '@/contexts/EffectContext';

interface FileUploadProps {
  onUploadComplete: (url: string) => void;
  accept?: string;
  maxSize?: number;
}

export function FileUpload({
  onUploadComplete,
  accept = 'image/*',
  maxSize = 5 * 1024 * 1024
}: FileUploadProps) {
  const layer = useEffectLayer();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [progress, setProgress] = useState<UploadProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];

    if (!selectedFile) return;

    // Validate file size
    if (selectedFile.size > maxSize) {
      setError(`File size must be less than ${maxSize / 1024 / 1024}MB`);
      return;
    }

    setFile(selectedFile);
    setError(null);

    // Generate preview for images
    if (selectedFile.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (e) => {
        setPreview(e.target?.result as string);
      };
      reader.readAsDataURL(selectedFile);
    }
  };

  const handleUpload = async () => {
    if (!file) return;

    setUploading(true);
    setError(null);
    setProgress(null);

    const upload = Effect.gen(function* (_) {
      const uploadService = yield* _(FileUploadService);

      const url = yield* _(
        uploadService.upload(file, (prog) => {
          setProgress(prog);
        })
      );

      return url;
    }).pipe(Effect.provide(layer));

    try {
      const url = await Effect.runPromise(upload);
      onUploadComplete(url);

      // Reset
      setFile(null);
      setPreview(null);
      setProgress(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setUploading(false);
    }
  };

  const handleRemove = () => {
    setFile(null);
    setPreview(null);
    setProgress(null);
    setError(null);
  };

  return (
    <div className="file-upload">
      {/* File input */}
      {!file && (
        <div className="file-input-container">
          <input
            type="file"
            onChange={handleFileChange}
            accept={accept}
            className="file-input"
          />
          <div className="file-input-placeholder">
            <p>Click to select file or drag and drop</p>
            <p className="text-muted">Max size: {maxSize / 1024 / 1024}MB</p>
          </div>
        </div>
      )}

      {/* Preview */}
      {file && preview && (
        <div className="file-preview">
          <img src={preview} alt="Preview" />
          <div className="file-info">
            <p>{file.name}</p>
            <p>{(file.size / 1024).toFixed(2)} KB</p>
          </div>
        </div>
      )}

      {/* Progress */}
      {uploading && progress && (
        <div className="upload-progress">
          <div
            className="progress-bar"
            style={{ width: `${progress.percentage}%` }}
          />
          <p>{progress.percentage}% uploaded</p>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="error-message">{error}</div>
      )}

      {/* Actions */}
      {file && !uploading && (
        <div className="file-actions">
          <button
            onClick={handleUpload}
            className="btn btn-primary"
          >
            Upload
          </button>
          <button
            onClick={handleRemove}
            className="btn btn-secondary"
          >
            Remove
          </button>
        </div>
      )}
    </div>
  );
}
```

---

## 11.6 Cross-field Validation

### 11.6.1 Schema Transformations

บางครั้งเราต้อง validate ที่ต้องเช็ค relationship ระหว่าง fields

**Example: Password Confirmation**

```typescript
import { Schema } from '@effect/schema';

const PasswordFormSchema = Schema.struct({
  password: Schema.string.pipe(
    Schema.minLength(8, {
      message: () => 'Password must be at least 8 characters'
    }),
    Schema.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/, {
      message: () => 'Password must contain uppercase, lowercase, and number'
    })
  ),
  confirmPassword: Schema.string
}).pipe(
  // Cross-field validation
  Schema.filter(
    ({ password, confirmPassword }) => password === confirmPassword,
    {
      message: () => 'Passwords do not match'
    }
  )
);
```

**Example: Date Range**

```typescript
const DateRangeSchema = Schema.struct({
  startDate: Schema.Date,
  endDate: Schema.Date
}).pipe(
  Schema.filter(
    ({ startDate, endDate }) => startDate < endDate,
    {
      message: () => 'End date must be after start date'
    }
  )
);
```

**Example: Conditional Required Fields**

```typescript
const ShippingFormSchema = Schema.struct({
  sameAsB billing: Schema.boolean,
  shippingAddress: Schema.optional(
    Schema.struct({
      street: Schema.string,
      city: Schema.string,
      zipCode: Schema.string
    })
  )
}).pipe(
  // If sameAsBilling is false, shippingAddress is required
  Schema.filter(
    ({ sameAsBilling, shippingAddress }) =>
      sameAsBilling || shippingAddress !== undefined,
    {
      message: () => 'Shipping address is required'
    }
  )
);
```

---

## 11.7 Best Practices

### 11.7.1 Schema Organization

**DO: Split schemas into reusable pieces**

```typescript
// ✅ Good: Reusable schemas
const EmailSchema = Schema.string.pipe(
  Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/, {
    message: () => 'Invalid email'
  })
);

const PhoneSchema = Schema.string.pipe(
  Schema.pattern(/^[0-9]{10}$/, {
    message: () => 'Phone must be 10 digits'
  })
);

// Compose into larger schemas
const UserSchema = Schema.struct({
  email: EmailSchema,
  phone: PhoneSchema
});
```

**DON'T: Duplicate validation logic**

```typescript
// ❌ Bad: Duplicated patterns
const UserSchema = Schema.struct({
  email: Schema.string.pipe(
    Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/)
  )
});

const ContactSchema = Schema.struct({
  email: Schema.string.pipe(
    Schema.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/) // Duplicated!
  )
});
```

### 11.7.2 Error Messages

**DO: Provide helpful error messages**

```typescript
// ✅ Good: Clear, actionable messages
const PasswordSchema = Schema.string.pipe(
  Schema.minLength(8, {
    message: () => 'Password must be at least 8 characters long'
  }),
  Schema.pattern(/^(?=.*[A-Z])/, {
    message: () => 'Password must contain at least one uppercase letter'
  }),
  Schema.pattern(/^(?=.*[0-9])/, {
    message: () => 'Password must contain at least one number'
  })
);
```

### 11.7.3 Performance

**DO: Memoize schemas**

```typescript
// ✅ Good: Memoize expensive schemas
const TodoFormSchema = useMemo(
  () => Schema.struct({
    title: Schema.string.pipe(Schema.minLength(3)),
    // ... other fields
  }),
  []
);
```

**DON'T: Create schemas in render**

```typescript
// ❌ Bad: New schema every render
function MyForm() {
  const schema = Schema.struct({
    // Schema created every render!
  });
}
```

---

## 11.8 สรุป

### สิ่งที่ได้เรียนรู้ในบทนี้

1. **Effect Schema**
   - Schema definition
   - Built-in validators
   - Type inference
   - Decode and validation

2. **Complex Forms**
   - useFormSchema hook
   - Dynamic array fields
   - Nested objects
   - File uploads

3. **Multi-step Forms**
   - Wizard pattern
   - Step validation
   - Progress tracking
   - Navigation

4. **Advanced Patterns**
   - Cross-field validation
   - Conditional validation
   - Schema composition
   - Custom validators

### ข้อดีของ Schema-based Validation

1. **Declarative** - อ่านง่าย เข้าใจง่าย
2. **Type-Safe** - Auto type inference
3. **Reusable** - แชร์ schemas ได้
4. **Composable** - ประกอบ schemas ซับซ้อนได้
5. **Maintainable** - แก้ไขง่าย

### บทถัดไป

ในบทที่ 12 เราจะเรียนรู้:
- **Testing Frontend** - Unit tests, Integration tests
- **Testing Forms** - Schema validation tests
- **Testing Effects** - Mock services
- **E2E Testing** - Playwright/Cypress

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Product Form Schema

สร้าง schema สำหรับ product form:
- name (required, 3-100 chars)
- price (positive number, max 1,000,000)
- category (enum: electronics, clothing, food, other)
- tags (array of strings, max 10 tags)
- images (array of File, max 5 images, each < 2MB)

### ข้อ 2: Multi-step Checkout

สร้าง 3-step checkout wizard:
- Step 1: Shipping address
- Step 2: Payment method
- Step 3: Review & confirm

### ข้อ 3: Profile Form with Avatar

สร้าง profile form ที่มี:
- Name, email, phone
- Avatar upload with preview
- Bio (optional, max 500 chars)
- Social links (optional)

### ข้อ 4: Dynamic Survey Form

สร้าง survey form ที่:
- User สามารถเพิ่ม/ลบคำถามได้
- แต่ละคำถามมี type (text, number, choice)
- Validate ตาม type

### ข้อ 5: Date Range Picker

สร้าง date range picker component:
- Start date และ End date
- Validate: End > Start
- Max range: 90 days
- Disable past dates

---

**พร้อมที่จะสร้าง Production-Grade Forms แล้วใช่ไหม?**
