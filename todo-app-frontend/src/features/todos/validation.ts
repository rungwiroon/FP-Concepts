import { Either } from 'effect';
import type { CreateTodoRequest } from './types';

export interface ValidationError {
  field: string;
  message: string;
}

export type ValidationResult<A> = Either.Either<A, ValidationError[]>;

const validateTitle = (title: string): ValidationResult<string> =>
  title.trim().length > 0 && title.length <= 200
    ? Either.right(title)
    : Either.left([{
        field: 'title',
        message: 'Title is required and must be less than 200 characters'
      }]);

const validateDescription = (description: string | null): ValidationResult<string | null> =>
  description === null || description.length <= 1000
    ? Either.right(description)
    : Either.left([{
        field: 'description',
        message: 'Description must be less than 1000 characters'
      }]);

export const validateTodo = (
  input: CreateTodoRequest
): ValidationResult<CreateTodoRequest> => {
  const titleResult = validateTitle(input.title);
  const descResult = validateDescription(input.description);

  // Combine validation results - collect all errors
  if (Either.isLeft(titleResult) || Either.isLeft(descResult)) {
    const errors: ValidationError[] = [
      ...(Either.isLeft(titleResult) ? titleResult.left : []),
      ...(Either.isLeft(descResult) ? descResult.left : []),
    ];
    return Either.left(errors);
  }

  return Either.right(input);
};
