import { describe, expect, it } from 'vitest';
import { ClassBuilder, joinClasses, whenClass } from './class-builder';

describe('ClassBuilder', () => {
  describe('create and add', () => {
    it('should create empty builder', () => {
      const builder = ClassBuilder.create();
      expect(builder.build()).toBe('');
    });

    it('should create with initial classes', () => {
      const builder = ClassBuilder.create('flex', 'items-center');
      expect(builder.build()).toBe('flex items-center');
    });

    it('should add classes', () => {
      const result = ClassBuilder.create()
        .add('flex', 'items-center')
        .add('gap-4', 'p-6')
        .build();
      expect(result).toBe('flex items-center gap-4 p-6');
    });

    it('should ignore null and undefined', () => {
      const result = ClassBuilder.create()
        .add('flex', null, 'items-center', undefined)
        .build();
      expect(result).toBe('flex items-center');
    });

    it('should ignore duplicates', () => {
      const result = ClassBuilder.create()
        .add('flex', 'items-center')
        .add('flex', 'gap-4')
        .build();
      expect(result).toBe('flex items-center gap-4');
    });
  });

  describe('when', () => {
    it('should add classes when condition is true', () => {
      const result = ClassBuilder.create()
        .add('flex')
        .when(true, 'bg-blue-600', 'bg-gray-600')
        .build();
      expect(result).toBe('flex bg-blue-600');
    });

    it('should add fallback when condition is false', () => {
      const result = ClassBuilder.create()
        .add('flex')
        .when(false, 'bg-blue-600', 'bg-gray-600')
        .build();
      expect(result).toBe('flex bg-gray-600');
    });

    it('should add nothing when false and no fallback', () => {
      const result = ClassBuilder.create()
        .add('flex')
        .when(false, 'bg-blue-600')
        .build();
      expect(result).toBe('flex');
    });

    it('should support multiple classes when true', () => {
      const result = ClassBuilder.create()
        .when(true, 'flex', 'items-center', 'gap-4')
        .build();
      expect(result).toBe('flex items-center gap-4');
    });
  });

  describe('withPrefix', () => {
    it('should add prefix to classes', () => {
      const result = ClassBuilder.create()
        .withPrefix('hover:', 'bg-gray-100', 'scale-110')
        .build();
      expect(result).toBe('hover:bg-gray-100 hover:scale-110');
    });
  });

  describe('state variants', () => {
    it('should add hover classes', () => {
      const result = ClassBuilder.create()
        .add('bg-white')
        .hover('bg-gray-100', 'scale-105')
        .build();
      expect(result).toBe('bg-white hover:bg-gray-100 hover:scale-105');
    });

    it('should add focus classes', () => {
      const result = ClassBuilder.create()
        .add('border-gray-300')
        .focus('ring-2', 'ring-blue-500')
        .build();
      expect(result).toBe('border-gray-300 focus:ring-2 focus:ring-blue-500');
    });

    it('should add active classes', () => {
      const result = ClassBuilder.create()
        .add('bg-blue-600')
        .active('bg-blue-700')
        .build();
      expect(result).toBe('bg-blue-600 active:bg-blue-700');
    });

    it('should add dark mode classes', () => {
      const result = ClassBuilder.create()
        .add('bg-white', 'text-gray-900')
        .dark('bg-gray-900', 'text-white')
        .build();
      expect(result).toBe('bg-white text-gray-900 dark:bg-gray-900 dark:text-white');
    });

    it('should add group-hover classes', () => {
      const result = ClassBuilder.create()
        .add('opacity-0')
        .groupHover('opacity-100')
        .build();
      expect(result).toBe('opacity-0 group-hover:opacity-100');
    });

    it('should add focus-within classes', () => {
      const result = ClassBuilder.create()
        .add('border-gray-300')
        .focusWithin('border-blue-500')
        .build();
      expect(result).toBe('border-gray-300 focus-within:border-blue-500');
    });
  });

  describe('responsive breakpoints', () => {
    it('should add sm breakpoint classes', () => {
      const result = ClassBuilder.create()
        .add('text-sm')
        .sm('text-base')
        .build();
      expect(result).toBe('text-sm sm:text-base');
    });

    it('should add md breakpoint classes', () => {
      const result = ClassBuilder.create()
        .add('flex-col')
        .md('flex-row')
        .build();
      expect(result).toBe('flex-col md:flex-row');
    });

    it('should add lg breakpoint classes', () => {
      const result = ClassBuilder.create()
        .add('hidden')
        .lg('block')
        .build();
      expect(result).toBe('hidden lg:block');
    });

    it('should add xl breakpoint classes', () => {
      const result = ClassBuilder.create()
        .add('max-w-md')
        .xl('max-w-lg')
        .build();
      expect(result).toBe('max-w-md xl:max-w-lg');
    });

    it('should add 2xl breakpoint classes', () => {
      const result = ClassBuilder.create()
        .add('px-4')
        .xl2('px-8')
        .build();
      expect(result).toBe('px-4 2xl:px-8');
    });

    it('should add custom breakpoint classes', () => {
      const result = ClassBuilder.create()
        .add('hidden')
        .responsive('3xl', 'block')
        .build();
      expect(result).toBe('hidden 3xl:block');
    });
  });

  describe('toString', () => {
    it('should convert to string automatically', () => {
      const builder = ClassBuilder.create('flex', 'items-center');
      expect(builder.toString()).toBe('flex items-center');
      expect(String(builder)).toBe('flex items-center');
    });
  });

  describe('chaining', () => {
    it('should support complex chaining', () => {
      const result = ClassBuilder.create('flex', 'items-center')
        .add('gap-4', 'p-6')
        .when(true, 'bg-white')
        .hover('bg-gray-100')
        .dark('bg-gray-900')
        .md('flex-row')
        .build();
      expect(result).toBe('flex items-center gap-4 p-6 bg-white hover:bg-gray-100 dark:bg-gray-900 md:flex-row');
    });
  });
});

describe('joinClasses', () => {
  it('should join classes', () => {
    expect(joinClasses('flex', 'items-center', 'gap-4')).toBe('flex items-center gap-4');
  });

  it('should filter null and undefined', () => {
    expect(joinClasses('flex', null, 'items-center', undefined, 'gap-4')).toBe('flex items-center gap-4');
  });

  it('should filter empty strings', () => {
    expect(joinClasses('flex', '', 'items-center', '  ', 'gap-4')).toBe('flex items-center gap-4');
  });
});

describe('whenClass', () => {
  it('should return className when true', () => {
    expect(whenClass(true, 'text-blue-600', 'text-gray-600')).toBe('text-blue-600');
  });

  it('should return fallback when false', () => {
    expect(whenClass(false, 'text-blue-600', 'text-gray-600')).toBe('text-gray-600');
  });

  it('should return undefined when false and no fallback', () => {
    expect(whenClass(false, 'text-blue-600')).toBeUndefined();
  });
});
